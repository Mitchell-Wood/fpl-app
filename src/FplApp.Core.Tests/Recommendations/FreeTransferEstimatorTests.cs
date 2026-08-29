using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class FreeTransferEstimatorTests
{
    private static EntryHistory Gw(int @event, int transfers) => new()
    {
        Event = @event,
        EventTransfers = transfers,
    };

    private static ChipPlay Chip(string name, int @event) => new()
    {
        Name = name,
        Event = @event,
    };

    [Fact]
    public void NoHistory_StartsAtOneFreeTransfer()
    {
        var result = FreeTransferEstimator.EstimateAvailable([], []);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Gameweek1_IsIgnored_EvenIfItRecordsTransfers()
    {
        // GW1 is squad selection, not a transfer week, so it must not affect the count.
        var history = new List<EntryHistory> { Gw(1, 15) };

        var result = FreeTransferEstimator.EstimateAvailable(history, []);

        Assert.Equal(1, result);
    }

    [Fact]
    public void UnusedTransfers_AccrueOneStepPerGameweek()
    {
        var history = new List<EntryHistory>
        {
            Gw(2, 0),
            Gw(3, 0),
            Gw(4, 0),
        };

        var result = FreeTransferEstimator.EstimateAvailable(history, []);

        // 1 (baseline) + 1 + 1 + 1 = 4
        Assert.Equal(4, result);
    }

    [Fact]
    public void BankedTransfers_CapAtFive()
    {
        var history = Enumerable.Range(2, 10).Select(gw => Gw(gw, 0)).ToList();

        var result = FreeTransferEstimator.EstimateAvailable(history, []);

        Assert.Equal(5, result);
    }

    [Fact]
    public void UsingTransfersWithinBankedAmount_DecrementsThenAccrues()
    {
        var history = new List<EntryHistory>
        {
            Gw(2, 0), // available: 1 -> 2
            Gw(3, 0), // available: 2 -> 3
            Gw(4, 2), // used 2 of 3 banked: available -> min(5, 3 - 2 + 1) = 2
        };

        var result = FreeTransferEstimator.EstimateAvailable(history, []);

        Assert.Equal(2, result);
    }

    [Fact]
    public void TakingAHitBeyondBankedTransfers_ResetsToBaselineOfOne()
    {
        var history = new List<EntryHistory>
        {
            Gw(2, 0), // available: 1 -> 2
            Gw(3, 4), // took a hit (4 > 2 banked): resets to 1
        };

        var result = FreeTransferEstimator.EstimateAvailable(history, []);

        Assert.Equal(1, result);
    }

    [Theory]
    [InlineData("wildcard")]
    [InlineData("Wildcard")]
    [InlineData("freehit")]
    [InlineData("FreeHit")]
    public void UnlimitedTransferChip_FreezesBankedTransfers_IgnoringTransferCount(string chipName)
    {
        var history = new List<EntryHistory>
        {
            Gw(2, 0),  // available: 1 -> 2
            Gw(3, 99), // chip played this week, so the transfer count must be ignored entirely
        };
        var chips = new List<ChipPlay> { Chip(chipName, 3) };

        var result = FreeTransferEstimator.EstimateAvailable(history, chips);

        // Chip week is frozen: no gain, no loss, so the banked amount carries over unchanged.
        Assert.Equal(2, result);
    }

    [Fact]
    public void UnlimitedTransferChip_AlsoCapsAtFive()
    {
        var history = Enumerable.Range(2, 6).Select(gw => Gw(gw, 0)).ToList();
        history.Add(Gw(8, 20));
        var chips = new List<ChipPlay> { Chip("wildcard", 8) };

        var result = FreeTransferEstimator.EstimateAvailable(history, chips);

        Assert.Equal(5, result);
    }

    [Fact]
    public void OtherChips_DoNotPreserveTransfers_AndAreSubjectToTheNormalHitRule()
    {
        // "bboost" and "3xc" don't grant unlimited transfers, so a big transfer count that
        // gameweek must still be treated as a hit under the normal rule.
        var history = new List<EntryHistory>
        {
            Gw(2, 0), // available: 1 -> 2
            Gw(3, 5), // bench boost played, but transfers still count: 5 > 2 banked -> reset to 1
        };
        var chips = new List<ChipPlay> { Chip("bboost", 3) };

        var result = FreeTransferEstimator.EstimateAvailable(history, chips);

        Assert.Equal(1, result);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => FreeTransferEstimator.EstimateAvailable(null!, []));
        Assert.Throws<ArgumentNullException>(() => FreeTransferEstimator.EstimateAvailable([], null!));
    }
}
