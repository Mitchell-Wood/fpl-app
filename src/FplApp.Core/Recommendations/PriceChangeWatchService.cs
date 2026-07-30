using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Surfaces today's actual price changes and transfer-momentum trends. FPL doesn't publish the
/// exact thresholds it uses to trigger a price rise/fall, so this doesn't try to predict one —
/// it reports what's already happened today, plus which not-yet-moved players have the heaviest
/// net transfer activity (the leading signal a change is more likely soon).
/// </summary>
public class PriceChangeWatchService
{
    public PriceWatchResult GetPriceWatch(BootstrapStatic bootstrap, int count = 15)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);

        var all = bootstrap.Elements.Select(p => new PriceWatchPlayer
        {
            PlayerId = p.Id,
            WebName = p.WebName,
            TeamName = teamsById.GetValueOrDefault(p.Team)?.ShortName ?? "?",
            ElementType = p.ElementType,
            NowCost = p.NowCost,
            SelectedByPercent = ParseDecimal(p.SelectedByPercent),
            CostChangeEventTenths = p.CostChangeEvent,
            TransfersInEvent = p.TransfersInEvent,
            TransfersOutEvent = p.TransfersOutEvent,
            NetTransfersEvent = p.TransfersInEvent - p.TransfersOutEvent,
        }).ToList();

        var risersToday = all
            .Where(p => p.CostChangeEventTenths > 0)
            .OrderByDescending(p => p.CostChangeEventTenths)
            .ThenByDescending(p => p.NetTransfersEvent)
            .Take(count)
            .ToList();

        var fallersToday = all
            .Where(p => p.CostChangeEventTenths < 0)
            .OrderBy(p => p.CostChangeEventTenths)
            .ThenBy(p => p.NetTransfersEvent)
            .Take(count)
            .ToList();

        // "Trending" only makes sense for players that haven't already moved today.
        var unchanged = all.Where(p => p.CostChangeEventTenths == 0);

        var trendingIn = unchanged
            .Where(p => p.NetTransfersEvent > 0)
            .OrderByDescending(p => p.NetTransfersEvent)
            .Take(count)
            .ToList();

        var trendingOut = unchanged
            .Where(p => p.NetTransfersEvent < 0)
            .OrderBy(p => p.NetTransfersEvent)
            .Take(count)
            .ToList();

        return new PriceWatchResult
        {
            RisersToday = risersToday,
            FallersToday = fallersToday,
            TrendingIn = trendingIn,
            TrendingOut = trendingOut,
        };
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}

public class PriceWatchResult
{
    public IReadOnlyList<PriceWatchPlayer> RisersToday { get; set; } = [];
    public IReadOnlyList<PriceWatchPlayer> FallersToday { get; set; } = [];
    public IReadOnlyList<PriceWatchPlayer> TrendingIn { get; set; } = [];
    public IReadOnlyList<PriceWatchPlayer> TrendingOut { get; set; } = [];
}
