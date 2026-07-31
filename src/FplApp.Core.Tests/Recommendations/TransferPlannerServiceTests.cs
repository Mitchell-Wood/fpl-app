using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class TransferPlannerServiceTests
{
    // Every player in these tests lives on team 1, which has a single upcoming fixture at
    // difficulty 3 (average) against team 2. Difficulty 3 gives a fixture factor of exactly 1,
    // so a player's projected points and score collapse to just their form/cost inputs — that
    // makes the arithmetic in each test easy to verify by hand.
    private const int LookaheadWeeks = 1;

    private static readonly IReadOnlyList<Fixture> Fixtures =
    [
        new() { Event = 1, Finished = false, TeamH = 1, TeamHDifficulty = 3, TeamA = 2, TeamADifficulty = 3 },
    ];

    private static Player MakePlayer(int id, int elementType, int nowCost, string form, string status = "a")
        => new()
        {
            Id = id,
            WebName = $"Player{id}",
            Team = 1,
            ElementType = elementType,
            NowCost = nowCost,
            Form = form,
            Status = status,
        };

    private static SquadPickAnalysis MakeSquadPick(Player player)
        => new()
        {
            PlayerId = player.Id,
            WebName = player.WebName,
            TeamName = "T1",
            ElementType = player.ElementType,
            NowCost = player.NowCost,
        };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new()
        {
            Teams = [new Team { Id = 1, ShortName = "T1" }, new Team { Id = 2, ShortName = "T2" }],
            Elements = players.ToList(),
        };

    private static TransferPlannerService MakeService() => new(new PlayerRecommendationService());

    [Fact]
    public void SuggestTransfers_RanksByPointsGainDescending_IncludingNegativeGainEntries()
    {
        // Distinct element types per squad player so each one's candidate pool is isolated.
        var out1 = MakePlayer(101, elementType: 2, nowCost: 50, form: "1.0");
        var in1 = MakePlayer(201, elementType: 2, nowCost: 50, form: "9.0"); // gain +8.0

        var out2 = MakePlayer(102, elementType: 3, nowCost: 50, form: "5.0");
        var in2 = MakePlayer(202, elementType: 3, nowCost: 50, form: "4.0"); // gain -1.0

        var out3 = MakePlayer(103, elementType: 4, nowCost: 50, form: "3.0");
        var in3 = MakePlayer(203, elementType: 4, nowCost: 50, form: "3.5"); // gain +0.5

        var bootstrap = MakeBootstrap(out1, in1, out2, in2, out3, in3);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(out1), MakeSquadPick(out2), MakeSquadPick(out3) };

        var result = MakeService().SuggestTransfers(bootstrap, Fixtures, squad, bank: 0, LookaheadWeeks, candidatesPerPlayer: 3, maxSuggestions: 3);

        Assert.Equal([101, 103, 102], result.Select(s => s.OutPlayerId));
        Assert.Equal(8.0, result[0].ExpectedPointsGain);
        Assert.Equal(0.5, result[1].ExpectedPointsGain);
        Assert.Equal(-1.0, result[2].ExpectedPointsGain);
    }

    [Fact]
    public void SuggestTransfers_OmitsSquadPlayer_WhenNoAffordableCandidateExists()
    {
        var owned = MakePlayer(104, elementType: 2, nowCost: 50, form: "1.0");
        var tooExpensive = MakePlayer(204, elementType: 2, nowCost: 200, form: "9.0");

        var bootstrap = MakeBootstrap(owned, tooExpensive);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned) };

        var result = MakeService().SuggestTransfers(bootstrap, Fixtures, squad, bank: 0, LookaheadWeeks);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildTransferPlan_RecommendsAnAffordablePositiveGainTransfer()
    {
        var owned = MakePlayer(101, elementType: 2, nowCost: 50, form: "2.0");
        var upgrade = MakePlayer(201, elementType: 2, nowCost: 50, form: "6.0"); // gain +4.0, netCost 0

        var bootstrap = MakeBootstrap(owned, upgrade);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned) };

        var plan = MakeService().BuildTransferPlan(bootstrap, Fixtures, squad, bank: 0, LookaheadWeeks, freeTransfersAvailable: 1);

        var suggestion = Assert.Single(plan.RecommendedTransfers);
        Assert.Equal(101, suggestion.OutPlayerId);
        Assert.Equal(201, suggestion.Candidates[0].PlayerId);
        Assert.Equal(4.0, suggestion.ExpectedPointsGain);
        Assert.Equal(1, plan.FreeTransfersUsed);
        Assert.Equal(0, plan.FreeTransfersToBank);
        Assert.Equal(4.0, plan.TotalExpectedPointsGain);
        Assert.Null(plan.HitCandidate);
    }

    [Fact]
    public void BuildTransferPlan_NeverRecommendsANonPositiveGainTransfer_EvenWhenFree()
    {
        var owned = MakePlayer(101, elementType: 2, nowCost: 50, form: "6.0"); // already strong
        var worseAlternative = MakePlayer(201, elementType: 2, nowCost: 50, form: "2.0"); // gain -4.0

        var bootstrap = MakeBootstrap(owned, worseAlternative);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned) };

        var plan = MakeService().BuildTransferPlan(bootstrap, Fixtures, squad, bank: 0, LookaheadWeeks, freeTransfersAvailable: 1);

        Assert.Empty(plan.RecommendedTransfers);
        Assert.Equal(0, plan.FreeTransfersUsed);
        Assert.Equal(1, plan.FreeTransfersToBank);
        Assert.Null(plan.HitCandidate); // a negative-gain transfer isn't offered as a hit either
    }

    [Fact]
    public void BuildTransferPlan_DropsATransferThatDoesNotFitTheSharedBudgetPool_EvenIfIndividuallyAffordable()
    {
        // Both transfers are affordable against the player's own price alone, but the shared
        // pool (bank only, here) can't cover both once the higher-gain one is taken first.
        var owned1 = MakePlayer(101, elementType: 2, nowCost: 50, form: "1.0");
        var upgrade1 = MakePlayer(201, elementType: 2, nowCost: 80, form: "9.0"); // gain +8.0, netCost 30

        var owned2 = MakePlayer(102, elementType: 3, nowCost: 50, form: "1.0");
        var upgrade2 = MakePlayer(202, elementType: 3, nowCost: 70, form: "7.0"); // gain +6.0, netCost 20

        var bootstrap = MakeBootstrap(owned1, upgrade1, owned2, upgrade2);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned1), MakeSquadPick(owned2) };

        var plan = MakeService().BuildTransferPlan(bootstrap, Fixtures, squad, bank: 30, LookaheadWeeks, freeTransfersAvailable: 2);

        var suggestion = Assert.Single(plan.RecommendedTransfers);
        Assert.Equal(101, suggestion.OutPlayerId);
        Assert.Equal(1, plan.FreeTransfersUsed);
        Assert.Equal(1, plan.FreeTransfersToBank);
        // The dropped transfer isn't offered as a hit candidate either — it's simply unaffordable.
        Assert.Null(plan.HitCandidate);
    }

    [Fact]
    public void BuildTransferPlan_OffersTheNextBestTransferAsAHitCandidate_WhenFreeTransfersAreExhausted()
    {
        var owned1 = MakePlayer(101, elementType: 2, nowCost: 50, form: "1.0");
        var upgrade1 = MakePlayer(201, elementType: 2, nowCost: 60, form: "9.0"); // gain +8.0

        var owned2 = MakePlayer(102, elementType: 3, nowCost: 50, form: "1.0");
        var upgrade2 = MakePlayer(202, elementType: 3, nowCost: 60, form: "9.0"); // gain +8.0, but worth more than a hit's 4pt cost

        var bootstrap = MakeBootstrap(owned1, upgrade1, owned2, upgrade2);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned1), MakeSquadPick(owned2) };

        var plan = MakeService().BuildTransferPlan(bootstrap, Fixtures, squad, bank: 100, LookaheadWeeks, freeTransfersAvailable: 1);

        Assert.Single(plan.RecommendedTransfers);
        Assert.NotNull(plan.HitCandidate);
        Assert.Equal(4.0, plan.HitCandidateNetGain); // 8.0 projected gain - 4 point hit cost
        Assert.True(plan.HitWorthIt);
    }

    [Fact]
    public void BuildTransferPlan_HitCandidate_NotWorthItWhenGainIsBelowFour()
    {
        var owned1 = MakePlayer(101, elementType: 2, nowCost: 50, form: "1.0");
        var upgrade1 = MakePlayer(201, elementType: 2, nowCost: 60, form: "9.0"); // gain +8.0

        var owned2 = MakePlayer(102, elementType: 3, nowCost: 50, form: "1.0");
        var upgrade2 = MakePlayer(202, elementType: 3, nowCost: 60, form: "4.0"); // gain +3.0, below the 4pt hit cost

        var bootstrap = MakeBootstrap(owned1, upgrade1, owned2, upgrade2);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned1), MakeSquadPick(owned2) };

        var plan = MakeService().BuildTransferPlan(bootstrap, Fixtures, squad, bank: 100, LookaheadWeeks, freeTransfersAvailable: 1);

        Assert.NotNull(plan.HitCandidate);
        Assert.Equal(-1.0, plan.HitCandidateNetGain);
        Assert.False(plan.HitWorthIt);
    }

    [Fact]
    public void SuggestFundedUpgrade_CombinesADowngradeAndAFundedUpgrade_WhenTheComboUnlocksABetterPlayer()
    {
        var downgradeFrom = MakePlayer(201, elementType: 2, nowCost: 60, form: "3.0");
        var downgradeTo = MakePlayer(301, elementType: 2, nowCost: 55, form: "3.0"); // frees up 5

        var upgradeFrom = MakePlayer(202, elementType: 3, nowCost: 50, form: "2.0");
        // Only affordable once the downgrade's freed-up money is added (50 standalone vs 55 funded).
        var upgradeTo = MakePlayer(302, elementType: 3, nowCost: 55, form: "8.0");

        var bootstrap = MakeBootstrap(downgradeFrom, downgradeTo, upgradeFrom, upgradeTo);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(downgradeFrom), MakeSquadPick(upgradeFrom) };

        var result = MakeService().SuggestFundedUpgrade(bootstrap, Fixtures, squad, bank: 0, LookaheadWeeks);

        Assert.NotNull(result);
        Assert.Equal(201, result!.Downgrade.OutPlayerId);
        Assert.Equal(301, result.Downgrade.InPlayerId);
        Assert.Equal(5, result.MoneySaved);
        Assert.Equal(202, result.Upgrade.OutPlayerId);
        Assert.Equal(302, result.Upgrade.InPlayerId);
        Assert.Equal(6.0, result.NetExpectedPointsGain); // (3.0-3.0) downgrade leg + (8.0-2.0) upgrade leg
    }

    [Fact]
    public void SuggestFundedUpgrade_ReturnsNull_WhenSquadHasOnlyOnePlayer()
    {
        // The upgrade leg always targets a *different* squad player than the downgrade leg, so a
        // single-player squad can never produce a combo.
        var onlyPlayer = MakePlayer(101, elementType: 2, nowCost: 50, form: "3.0");
        var cheaperAlternative = MakePlayer(201, elementType: 2, nowCost: 45, form: "3.0");

        var bootstrap = MakeBootstrap(onlyPlayer, cheaperAlternative);
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(onlyPlayer) };

        var result = MakeService().SuggestFundedUpgrade(bootstrap, Fixtures, squad, bank: 0, LookaheadWeeks);

        Assert.Null(result);
    }
}
