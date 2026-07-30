using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>Suggests players to transfer in based on form, value for money, and fixture ease.</summary>
public class PlayerRecommendationService
{
    /// <summary>
    /// Ranks available players by recent form, points-per-cost, and how easy their upcoming fixtures
    /// are, optionally restricted to one position.
    /// </summary>
    /// <param name="bootstrap">The bootstrap-static data to recommend from.</param>
    /// <param name="fixtures">All fixtures, used to weight players with easier upcoming runs.</param>
    /// <param name="elementTypeId">Optional position filter (1=GK, 2=DEF, 3=MID, 4=FWD).</param>
    /// <param name="count">Maximum number of players to return.</param>
    /// <param name="fixtureLookaheadWeeks">How many upcoming gameweeks of fixtures to factor in.</param>
    /// <param name="excludePlayerIds">Player ids to leave out, e.g. players already owned.</param>
    /// <param name="maxCost">Maximum now_cost (tenths of a million) a candidate may have.</param>
    public IReadOnlyList<Player> RecommendPlayers(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        int? elementTypeId = null,
        int count = 10,
        int fixtureLookaheadWeeks = 5,
        IReadOnlySet<int>? excludePlayerIds = null,
        int? maxCost = null)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);

        var difficultyByTeam = FixtureDifficultyCalculator.AverageUpcomingDifficultyByTeam(fixtures, fixtureLookaheadWeeks);

        var candidates = bootstrap.Elements
            .Where(p => p.Status == "a") // available (not injured/suspended/unavailable)
            .Where(p => elementTypeId is null || p.ElementType == elementTypeId)
            .Where(p => excludePlayerIds is null || !excludePlayerIds.Contains(p.Id))
            .Where(p => maxCost is null || p.NowCost <= maxCost);

        return candidates
            .OrderByDescending(p => Score(p, difficultyByTeam))
            .Take(count)
            .ToList();
    }

    private static double Score(Player player, IReadOnlyDictionary<int, double> difficultyByTeam)
    {
        var form = ParseDecimal(player.Form);
        var costInMillions = player.NowCost / 10.0;
        if (costInMillions <= 0)
        {
            return 0;
        }

        // Weight recent form more heavily than season-long value, since it's the better signal
        // for who to bring in next, but still reward players who are cheap for what they return.
        var valuePerCost = player.TotalPoints / costInMillions;
        var baseScore = (form * 2) + valuePerCost;

        // Scale by upcoming fixture ease: difficulty 3 (average) leaves the score unchanged,
        // easier runs boost it, tougher runs reduce it. Teams with no upcoming fixtures in the
        // window (or not found) are treated as average.
        var avgDifficulty = difficultyByTeam.GetValueOrDefault(player.Team, 3.0);
        var fixtureFactor = (6.0 - avgDifficulty) / 3.0;

        return baseScore * fixtureFactor;
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
