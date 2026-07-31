using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class BenchBoostPlannerServiceTests
{
    private const int TargetEvent = 5;

    private static Player MakePlayer(int id, int team, int elementType, int nowCost, string form, string status = "a")
        => new()
        {
            Id = id,
            WebName = $"Player{id}",
            Team = team,
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
            TeamName = "T",
            ElementType = player.ElementType,
            NowCost = player.NowCost,
        };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new()
        {
            Teams = Enumerable.Range(1, 20).Select(id => new Team { Id = id, ShortName = $"T{id}" }).ToList(),
            Elements = players.ToList(),
        };

    [Fact]
    public void PlanBenchBoost_ScoresOnlyTheTargetGameweek_HandlingBlankAndDoubleGameweeksWithinIt()
    {
        var blankAtTarget = MakePlayer(102, team: 2, elementType: 3, nowCost: 50, form: "5.0"); // no fixture in event 5
        var normalAtTarget = MakePlayer(101, team: 1, elementType: 2, nowCost: 50, form: "4.0"); // one fixture in event 5
        var doubleAtTarget = MakePlayer(103, team: 3, elementType: 4, nowCost: 50, form: "2.0"); // two fixtures in event 5

        var bootstrap = MakeBootstrap(blankAtTarget, normalAtTarget, doubleAtTarget);
        var fixtures = new List<Fixture>
        {
            new() { Event = TargetEvent, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 },
            new() { Event = TargetEvent, TeamH = 3, TeamHDifficulty = 3, TeamA = 10, TeamADifficulty = 3 },
            new() { Event = TargetEvent, TeamH = 11, TeamHDifficulty = 3, TeamA = 3, TeamADifficulty = 3 },
            new() { Event = TargetEvent + 1, TeamH = 2, TeamHDifficulty = 3, TeamA = 12, TeamADifficulty = 3 }, // team 2's fixture is a different gameweek
        };
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(normalAtTarget), MakeSquadPick(blankAtTarget), MakeSquadPick(doubleAtTarget) };

        var result = new BenchBoostPlannerService().PlanBenchBoost(bootstrap, fixtures, squad, bank: 0, TargetEvent, freeTransfersAvailable: 0);

        Assert.Equal(TargetEvent, result.EventId);
        // 4.0 (normal, factor 1) + 0 (blank) + 2.0 + 2.0 (double, factor 1 each) = 8.0
        Assert.Equal(8.0, result.CurrentSquadProjectedPoints);
        Assert.Equal(8.0, result.ProjectedSquadPointsAfterTransfers);
        Assert.Empty(result.Plan.RecommendedTransfers);
    }

    [Fact]
    public void PlanBenchBoost_RecommendsAnAffordableUpgrade_ForTheTargetGameweek()
    {
        var owned = MakePlayer(101, team: 1, elementType: 2, nowCost: 50, form: "1.0");
        var upgrade = MakePlayer(201, team: 1, elementType: 2, nowCost: 50, form: "6.0"); // same fixture, gain +5.0

        var bootstrap = MakeBootstrap(owned, upgrade);
        var fixtures = new List<Fixture> { new() { Event = TargetEvent, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 } };
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned) };

        var result = new BenchBoostPlannerService().PlanBenchBoost(bootstrap, fixtures, squad, bank: 0, TargetEvent, freeTransfersAvailable: 1);

        var suggestion = Assert.Single(result.Plan.RecommendedTransfers);
        Assert.Equal(101, suggestion.OutPlayerId);
        Assert.Equal(201, suggestion.Candidates[0].PlayerId);
        Assert.Equal(5.0, suggestion.ExpectedPointsGain);
        Assert.Equal(1.0, result.CurrentSquadProjectedPoints);
        Assert.Equal(6.0, result.ProjectedSquadPointsAfterTransfers);
        Assert.Equal(1, result.Plan.FreeTransfersUsed);
        Assert.Equal(0, result.Plan.FreeTransfersToBank);
    }

    [Fact]
    public void PlanBenchBoost_NeverRecommendsANonPositiveGainTransfer_EvenWhenFree()
    {
        var owned = MakePlayer(101, team: 1, elementType: 2, nowCost: 50, form: "6.0"); // already strong this week
        var worse = MakePlayer(201, team: 1, elementType: 2, nowCost: 50, form: "2.0");

        var bootstrap = MakeBootstrap(owned, worse);
        var fixtures = new List<Fixture> { new() { Event = TargetEvent, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 } };
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned) };

        var result = new BenchBoostPlannerService().PlanBenchBoost(bootstrap, fixtures, squad, bank: 0, TargetEvent, freeTransfersAvailable: 1);

        Assert.Empty(result.Plan.RecommendedTransfers);
        Assert.Null(result.Plan.HitCandidate);
    }

    [Fact]
    public void PlanBenchBoost_OffersTheNextBestTransferAsAHitCandidate_WhenFreeTransfersAreExhausted()
    {
        var owned1 = MakePlayer(101, team: 1, elementType: 2, nowCost: 50, form: "1.0");
        var upgrade1 = MakePlayer(201, team: 1, elementType: 2, nowCost: 60, form: "9.0"); // gain +8.0

        var owned2 = MakePlayer(102, team: 1, elementType: 3, nowCost: 50, form: "1.0");
        var upgrade2 = MakePlayer(202, team: 1, elementType: 3, nowCost: 60, form: "9.0"); // gain +8.0, well worth a hit

        var bootstrap = MakeBootstrap(owned1, upgrade1, owned2, upgrade2);
        var fixtures = new List<Fixture> { new() { Event = TargetEvent, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 } };
        var squad = new List<SquadPickAnalysis> { MakeSquadPick(owned1), MakeSquadPick(owned2) };

        var result = new BenchBoostPlannerService().PlanBenchBoost(bootstrap, fixtures, squad, bank: 100, TargetEvent, freeTransfersAvailable: 1);

        Assert.Single(result.Plan.RecommendedTransfers);
        Assert.NotNull(result.Plan.HitCandidate);
        Assert.Equal(4.0, result.Plan.HitCandidateNetGain); // 8.0 gain - 4pt hit cost
        Assert.True(result.Plan.HitWorthIt);
    }

    [Fact]
    public void PlanBenchBoost_ThrowsOnNullArguments()
    {
        var service = new BenchBoostPlannerService();
        var bootstrap = new BootstrapStatic();
        var squad = new List<SquadPickAnalysis>();

        Assert.Throws<ArgumentNullException>(() => service.PlanBenchBoost(null!, [], squad, bank: 0, TargetEvent, freeTransfersAvailable: 1));
        Assert.Throws<ArgumentNullException>(() => service.PlanBenchBoost(bootstrap, null!, squad, bank: 0, TargetEvent, freeTransfersAvailable: 1));
        Assert.Throws<ArgumentNullException>(() => service.PlanBenchBoost(bootstrap, [], null!, bank: 0, TargetEvent, freeTransfersAvailable: 1));
    }
}
