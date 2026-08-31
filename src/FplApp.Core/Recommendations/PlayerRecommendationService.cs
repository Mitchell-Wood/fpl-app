using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>Suggests players to transfer in based on form, expected points, and fixture ease.</summary>
public class PlayerRecommendationService
{
    /// <summary>
    /// Ranks available players by <see cref="ExpectedPointsEngine.EffectiveRate"/>, points-per-cost,
    /// and how easy their upcoming fixtures are, optionally restricted to one position.
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

        var rawDifficultyByTeam = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, fixtureLookaheadWeeks);
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);

        var candidates = bootstrap.Elements
            .Where(p => p.Status == "a") // available (not injured/suspended/unavailable)
            .Where(p => elementTypeId is null || p.ElementType == elementTypeId)
            .Where(p => excludePlayerIds is null || !excludePlayerIds.Contains(p.Id))
            .Where(p => maxCost is null || p.NowCost <= maxCost);

        return candidates
            .OrderByDescending(p => Score(p, rawDifficultyByTeam, teamsById))
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Scores a player by <see cref="ExpectedPointsEngine.EffectiveRate"/>, points-per-cost, and
    /// upcoming fixture ease — exposed internally so other services (e.g.
    /// <see cref="TransferPlannerService"/>) can compare a currently-owned player against
    /// candidates on the same footing.
    /// </summary>
    internal static double Score(
        Player player,
        IReadOnlyDictionary<int, List<FixtureDifficultyEntry>> rawDifficultyByTeam,
        IReadOnlyDictionary<int, Team> teamsById)
    {
        var costInMillions = player.NowCost / 10.0;
        if (costInMillions <= 0)
        {
            return 0;
        }

        var playerTeam = teamsById.GetValueOrDefault(player.Team);
        var effectiveRate = ExpectedPointsEngine.EffectiveRate(player, playerTeam);

        // The blended rate is the leading signal for "how good is this player right now" (weighted
        // x4 to keep it the dominant term, matching the old form*2+ep_next*2 weighting). Points-per-
        // cost is kept as a smaller tiebreaker so cheaper-for-the-same-output players still edge out
        // pricier twins.
        var valuePerCost = player.TotalPoints / costInMillions;
        var baseScore = (effectiveRate * 4) + valuePerCost;

        var entries = rawDifficultyByTeam.GetValueOrDefault(player.Team, []);
        var avgFixtureFactor = entries.Count > 0
            ? entries.Average(e => ExpectedPointsEngine.FixtureFactor(e.Difficulty, playerTeam, teamsById.GetValueOrDefault(e.OpponentTeamId), e.IsHome))
            : 1.0;

        return baseScore * avgFixtureFactor;
    }
}
