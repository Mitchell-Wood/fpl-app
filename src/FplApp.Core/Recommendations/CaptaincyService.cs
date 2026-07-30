using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>Ranks a manager's starting XI by expected points if captained this gameweek.</summary>
public class CaptaincyService
{
    public IReadOnlyList<CaptainSuggestion> SuggestCaptains(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        TeamPicks picks,
        int eventId)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(picks);

        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var eventFixtures = fixtures.Where(f => f.Event == eventId).ToList();

        var results = new List<CaptainSuggestion>();

        // Captaincy only makes sense for players actually in the starting XI (position 1-11).
        foreach (var pick in picks.Picks.Where(p => p.Position <= 11))
        {
            if (!playersById.TryGetValue(pick.Element, out var player) || player.Status != "a")
            {
                continue;
            }

            var teamFixtures = eventFixtures.Where(f => f.TeamH == player.Team || f.TeamA == player.Team).ToList();

            // Recent form is the best signal once the season has some games in; before that
            // (or for a player short on minutes) fall back to last season's points-per-game.
            var form = ParseDecimal(player.Form);
            var effectiveForm = form > 0 ? form : ParseDecimal(player.PointsPerGame);

            var fixtureDtos = new List<CaptainFixture>();
            double expectedPoints = 0;

            foreach (var fixture in teamFixtures)
            {
                var isHome = fixture.TeamH == player.Team;
                var opponentId = isHome ? fixture.TeamA : fixture.TeamH;
                var difficulty = isHome ? fixture.TeamHDifficulty : fixture.TeamADifficulty;

                fixtureDtos.Add(new CaptainFixture
                {
                    Opponent = teamsById.GetValueOrDefault(opponentId)?.ShortName ?? "?",
                    Venue = isHome ? "H" : "A",
                    Difficulty = difficulty,
                });

                // Same difficulty-scaling formula used for player recommendations: difficulty 3
                // (average) leaves the score unchanged, easier fixtures boost it.
                var fixtureFactor = (6.0 - difficulty) / 3.0;
                expectedPoints += effectiveForm * fixtureFactor;
            }

            results.Add(new CaptainSuggestion
            {
                PlayerId = player.Id,
                WebName = player.WebName,
                TeamName = teamsById.GetValueOrDefault(player.Team)?.ShortName ?? "?",
                IsCurrentCaptain = pick.IsCaptain,
                IsCurrentViceCaptain = pick.IsViceCaptain,
                ExpectedPoints = Math.Round(expectedPoints, 2),
                Fixtures = fixtureDtos,
                Note = fixtureDtos.Count switch
                {
                    0 => "Blank gameweek",
                    > 1 => "Double gameweek",
                    _ => null,
                },
            });
        }

        return results.OrderByDescending(r => r.ExpectedPoints).ToList();
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
