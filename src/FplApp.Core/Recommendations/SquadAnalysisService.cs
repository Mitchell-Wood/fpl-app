using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>Flags players in an existing squad worth considering for transfer out.</summary>
public class SquadAnalysisService
{
    public IReadOnlyList<SquadPickAnalysis> AnalyzeSquad(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        TeamPicks picks,
        int fixtureLookaheadWeeks = 5)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(picks);

        var difficultyByTeam = FixtureDifficultyCalculator.AverageUpcomingDifficultyByTeam(fixtures, fixtureLookaheadWeeks);
        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);

        // Once some fixtures have been played, a player's own form becomes a meaningful signal.
        // Before that, everyone's form is 0, so flagging "poor form" would be noise.
        var seasonStarted = fixtures.Any(f => f.Finished);

        var results = new List<SquadPickAnalysis>();

        foreach (var pick in picks.Picks.OrderBy(p => p.Position))
        {
            if (!playersById.TryGetValue(pick.Element, out var player))
            {
                continue;
            }

            var team = teamsById.GetValueOrDefault(player.Team);
            var avgDifficulty = difficultyByTeam.GetValueOrDefault(player.Team, 3.0);
            var form = ParseDecimal(player.Form);
            var flags = new List<string>();

            if (player.Status != "a")
            {
                flags.Add(DescribeUnavailability(player));
            }
            else if (player.ChanceOfPlayingNextRound is { } chance && chance < 100)
            {
                flags.Add($"Doubtful ({chance}% chance of playing)");
            }

            if (seasonStarted && form < 2.0)
            {
                flags.Add("Poor recent form");
            }

            if (avgDifficulty > 3.5)
            {
                flags.Add("Tough fixtures ahead");
            }

            results.Add(new SquadPickAnalysis
            {
                PlayerId = player.Id,
                WebName = player.WebName,
                TeamName = team?.ShortName ?? "?",
                ElementType = player.ElementType,
                IsCaptain = pick.IsCaptain,
                IsViceCaptain = pick.IsViceCaptain,
                Multiplier = pick.Multiplier,
                IsBenched = pick.Position > 11,
                NowCost = player.NowCost,
                Form = form,
                TotalPoints = player.TotalPoints,
                AvgUpcomingDifficulty = Math.Round(avgDifficulty, 2),
                Flags = flags,
            });
        }

        return results;
    }

    private static string DescribeUnavailability(Player player)
    {
        var suffix = string.IsNullOrWhiteSpace(player.News) ? "" : $" — {player.News}";
        return player.Status switch
        {
            "i" => $"Injured{suffix}",
            "s" => $"Suspended{suffix}",
            "d" => $"Doubtful{suffix}",
            _ => $"Unavailable{suffix}",
        };
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
