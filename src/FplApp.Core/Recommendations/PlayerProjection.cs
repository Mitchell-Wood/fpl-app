using System.Globalization;
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
    /// Recent form (or FPL's own expected-points-next-gameweek, or season points-per-game,
    /// whichever is the best signal available) applied to each upcoming fixture and scaled by
    /// that fixture's difficulty. Summing per-fixture — rather than using an average — means a
    /// blank gameweek contributes nothing and a double gameweek counts twice, automatically.
    /// </summary>
    public static double EstimateProjectedPoints(Player player, IReadOnlyDictionary<int, List<int>> rawDifficultiesByTeam)
    {
        var form = ParseDecimal(player.Form);
        var expectedPointsNext = ParseDecimal(player.ExpectedPointsNext);
        var pointsPerGame = ParseDecimal(player.PointsPerGame);
        var effectiveRate = form > 0 ? form : (expectedPointsNext > 0 ? expectedPointsNext : pointsPerGame);

        if (!rawDifficultiesByTeam.TryGetValue(player.Team, out var difficulties) || difficulties.Count == 0)
        {
            return 0;
        }

        return difficulties.Sum(difficulty => effectiveRate * ((6.0 - difficulty) / 3.0));
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
