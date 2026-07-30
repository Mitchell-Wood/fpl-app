using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>Suggests players to transfer in based on form, expected points, and fixture ease.</summary>
public class PlayerRecommendationService
{
    /// <summary>
    /// Ranks available players by recent form, FPL's own next-gameweek expected points, points-per-
    /// cost, and how easy their upcoming fixtures are, optionally restricted to one position.
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

    /// <summary>
    /// Scores a player by recent form, FPL's own next-gameweek expected points, points-per-cost,
    /// and upcoming fixture ease — exposed internally so other services (e.g.
    /// <see cref="TransferPlannerService"/>) can compare a currently-owned player against
    /// candidates on the same footing.
    /// </summary>
    internal static double Score(Player player, IReadOnlyDictionary<int, double> difficultyByTeam)
    {
        var form = ParseDecimal(player.Form);
        var expectedPointsNext = ParseDecimal(player.ExpectedPointsNext);
        var costInMillions = player.NowCost / 10.0;
        if (costInMillions <= 0)
        {
            return 0;
        }

        // Form and FPL's own next-gameweek expected-points estimate are weighted as the two
        // leading signals for "how good is this player right now" — ep_next matters especially
        // pre-season or right after a player's form has reset to 0, when recent form alone is
        // uninformative but FPL's model already has a prediction. Points-per-cost is kept as a
        // smaller tiebreaker so cheaper-for-the-same-output players still edge out pricier twins.
        var valuePerCost = player.TotalPoints / costInMillions;
        var baseScore = (form * 2) + (expectedPointsNext * 2) + valuePerCost;

        // Scale by upcoming fixture ease over the chosen lookahead window: difficulty 3 (average)
        // leaves the score unchanged, easier runs boost it, tougher runs reduce it. Teams with no
        // upcoming fixtures in the window (or not found) are treated as average.
        var avgDifficulty = difficultyByTeam.GetValueOrDefault(player.Team, 3.0);
        var fixtureFactor = (6.0 - avgDifficulty) / 3.0;

        return baseScore * fixtureFactor;
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
