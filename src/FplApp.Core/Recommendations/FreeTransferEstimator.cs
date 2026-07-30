using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Estimates a manager's currently banked free transfers by simulating the accrual rule across
/// their public gameweek history: +1 free transfer per week, capped at 5, preserved (not reset)
/// through a Wildcard or Free Hit gameweek. Confirmed for the 2026/27 season: max-5 rollover is
/// unchanged; the "preserved through chips" behavior was confirmed for 2025/26 and not contradicted
/// by anything newer, but isn't separately reconfirmed for this exact season — so this is a
/// best-effort estimate, not a guarantee.
/// </summary>
public static class FreeTransferEstimator
{
    private const int MaxBanked = 5;
    private static readonly HashSet<string> UnlimitedTransferChips = new(StringComparer.OrdinalIgnoreCase) { "wildcard", "freehit" };

    public static int EstimateAvailable(IReadOnlyList<EntryHistory> current, IReadOnlyList<ChipPlay> chips)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(chips);

        var chipEvents = chips
            .Where(c => UnlimitedTransferChips.Contains(c.Name))
            .Select(c => c.Event)
            .ToHashSet();

        // Everyone starts a season with 1 free transfer entering gameweek 2 (gameweek 1's initial
        // squad selection isn't a "transfer").
        var available = 1;

        foreach (var gw in current.Where(c => c.Event > 1).OrderBy(c => c.Event))
        {
            if (chipEvents.Contains(gw.Event))
            {
                available = Math.Min(MaxBanked, available + 1);
            }
            else if (gw.EventTransfers <= available)
            {
                available = Math.Min(MaxBanked, available - gw.EventTransfers + 1);
            }
            else
            {
                // Took a hit beyond banked transfers — back to the baseline of 1 next week.
                available = 1;
            }
        }

        return available;
    }
}
