using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>Suggests players to transfer in based on form and value for money.</summary>
public class PlayerRecommendationService
{
    /// <summary>
    /// Ranks available players by recent form and points-per-cost, optionally restricted to one position.
    /// </summary>
    /// <param name="bootstrap">The bootstrap-static data to recommend from.</param>
    /// <param name="elementTypeId">Optional position filter (1=GK, 2=DEF, 3=MID, 4=FWD).</param>
    /// <param name="count">Maximum number of players to return.</param>
    public IReadOnlyList<Player> RecommendPlayers(BootstrapStatic bootstrap, int? elementTypeId = null, int count = 10)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        var candidates = bootstrap.Elements
            .Where(p => p.Status == "a") // available (not injured/suspended/unavailable)
            .Where(p => elementTypeId is null || p.ElementType == elementTypeId);

        return candidates
            .OrderByDescending(Score)
            .Take(count)
            .ToList();
    }

    private static double Score(Player player)
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
        return (form * 2) + valuePerCost;
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
