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

            var playerTeam = teamsById.GetValueOrDefault(player.Team);
            var teamFixtures = eventFixtures.Where(f => f.TeamH == player.Team || f.TeamA == player.Team).ToList();

            var fixtureDtos = new List<CaptainFixture>();
            double expectedPoints = 0;

            foreach (var fixture in teamFixtures)
            {
                var isHome = fixture.TeamH == player.Team;
                var opponentId = isHome ? fixture.TeamA : fixture.TeamH;
                var difficulty = isHome ? fixture.TeamHDifficulty : fixture.TeamADifficulty;
                var opponentTeam = teamsById.GetValueOrDefault(opponentId);

                fixtureDtos.Add(new CaptainFixture
                {
                    Opponent = opponentTeam?.ShortName ?? "?",
                    Venue = isHome ? "H" : "A",
                    Difficulty = difficulty,
                });

                expectedPoints += ExpectedPointsEngine.EstimatePoints(player, playerTeam, difficulty, opponentTeam, isHome);
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
}
