using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Projects a player's points over a fixture lookahead window — shared by every service that
/// needs to compare "how many points is this player worth over the next N gameweeks" (transfer
/// suggestions, the squad builder, etc.) on the same footing.
/// </summary>
internal static class PlayerProjection
{
    /// <summary>
    /// Sums <see cref="ExpectedPointsEngine.EstimatePoints"/> across each upcoming fixture — summing
    /// per-fixture (rather than using an average) means a blank gameweek contributes nothing and a
    /// double gameweek counts twice, automatically.
    /// </summary>
    public static double EstimateProjectedPoints(
        Player player,
        IReadOnlyDictionary<int, List<FixtureDifficultyEntry>> rawDifficultiesByTeam,
        IReadOnlyDictionary<int, Team> teamsById)
    {
        if (!rawDifficultiesByTeam.TryGetValue(player.Team, out var entries) || entries.Count == 0)
        {
            return 0;
        }

        var playerTeam = teamsById.GetValueOrDefault(player.Team);
        var nextEvent = entries.Min(e => e.EventId);
        return entries.Sum(e => ExpectedPointsEngine.EstimatePoints(
            player, playerTeam, e.Difficulty, teamsById.GetValueOrDefault(e.OpponentTeamId), e.IsHome, weeksAhead: e.EventId - nextEvent));
    }
}
