using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class PriceChangeWatchServiceTests
{
    private static Player MakePlayer(int id, int costChangeEvent, int transfersIn, int transfersOut)
        => new()
        {
            Id = id,
            WebName = $"Player{id}",
            CostChangeEvent = costChangeEvent,
            TransfersInEvent = transfersIn,
            TransfersOutEvent = transfersOut,
        };

    [Fact]
    public void GetPriceWatch_OrdersRisersByChangeThenNetTransfers_AndFallersSymmetrically()
    {
        var riserBig = MakePlayer(1, costChangeEvent: 2, transfersIn: 1000, transfersOut: 100);   // net +900
        var riserTieHigherNet = MakePlayer(2, costChangeEvent: 1, transfersIn: 900, transfersOut: 100); // net +800
        var riserTieLowerNet = MakePlayer(3, costChangeEvent: 1, transfersIn: 500, transfersOut: 400);  // net +100
        var fallerBig = MakePlayer(4, costChangeEvent: -2, transfersIn: 100, transfersOut: 1000);  // net -900
        var fallerTieLowerNet = MakePlayer(5, costChangeEvent: -1, transfersIn: 100, transfersOut: 900); // net -800
        var fallerTieHigherNet = MakePlayer(6, costChangeEvent: -1, transfersIn: 400, transfersOut: 500); // net -100

        var bootstrap = new BootstrapStatic
        {
            Elements = [riserBig, riserTieHigherNet, riserTieLowerNet, fallerBig, fallerTieLowerNet, fallerTieHigherNet],
        };

        var result = new PriceChangeWatchService().GetPriceWatch(bootstrap);

        Assert.Equal([1, 2, 3], result.RisersToday.Select(p => p.PlayerId));
        Assert.Equal([4, 5, 6], result.FallersToday.Select(p => p.PlayerId));
    }

    [Fact]
    public void GetPriceWatch_TrendingListsOnlyIncludeUnchangedPlayers_SplitByNetTransferDirection()
    {
        var trendingIn = MakePlayer(1, costChangeEvent: 0, transfersIn: 700, transfersOut: 200);   // net +500
        var trendingOut = MakePlayer(2, costChangeEvent: 0, transfersIn: 100, transfersOut: 600);  // net -500
        var netZero = MakePlayer(3, costChangeEvent: 0, transfersIn: 100, transfersOut: 100);      // net 0, no trend
        var alreadyRisen = MakePlayer(4, costChangeEvent: 1, transfersIn: 900, transfersOut: 100);  // already moved today

        var bootstrap = new BootstrapStatic { Elements = [trendingIn, trendingOut, netZero, alreadyRisen] };

        var result = new PriceChangeWatchService().GetPriceWatch(bootstrap);

        Assert.Equal([1], result.TrendingIn.Select(p => p.PlayerId));
        Assert.Equal([2], result.TrendingOut.Select(p => p.PlayerId));
    }

    [Fact]
    public void GetPriceWatch_NetTransfersEvent_IsInMinusOut()
    {
        var player = MakePlayer(1, costChangeEvent: 1, transfersIn: 700, transfersOut: 200);

        var result = new PriceChangeWatchService().GetPriceWatch(new BootstrapStatic { Elements = [player] });

        Assert.Equal(500, result.RisersToday.Single().NetTransfersEvent);
    }

    [Fact]
    public void GetPriceWatch_RespectsTheCountLimit()
    {
        var players = Enumerable.Range(1, 5)
            .Select(i => MakePlayer(i, costChangeEvent: i, transfersIn: 0, transfersOut: 0))
            .ToList();

        var result = new PriceChangeWatchService().GetPriceWatch(new BootstrapStatic { Elements = players }, count: 2);

        Assert.Equal(2, result.RisersToday.Count);
        Assert.Equal([5, 4], result.RisersToday.Select(p => p.PlayerId));
    }
}
