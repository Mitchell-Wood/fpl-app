using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class PlayerRecommendationServiceTests
{
    private static Player MakePlayer(
        int id, int team, int elementType, int nowCost, string form,
        string epNext = "0", int totalPoints = 0, string status = "a")
        => new()
        {
            Id = id,
            WebName = $"Player{id}",
            Team = team,
            ElementType = elementType,
            NowCost = nowCost,
            Form = form,
            ExpectedPointsNext = epNext,
            TotalPoints = totalPoints,
            Status = status,
        };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new() { Elements = players.ToList() };

    private static readonly IReadOnlyList<Fixture> NoFixtures = [];

    [Fact]
    public void RecommendPlayers_ExcludesUnavailablePlayers_RegardlessOfScore()
    {
        var injured = MakePlayer(1, team: 1, elementType: 2, nowCost: 50, form: "9.0", status: "i");
        var available = MakePlayer(2, team: 1, elementType: 2, nowCost: 50, form: "1.0", status: "a");

        var result = new PlayerRecommendationService().RecommendPlayers(MakeBootstrap(injured, available), NoFixtures);

        Assert.Equal([2], result.Select(p => p.Id));
    }

    [Fact]
    public void RecommendPlayers_FiltersByElementType()
    {
        var defender = MakePlayer(1, team: 1, elementType: 2, nowCost: 50, form: "1.0");
        var forward = MakePlayer(2, team: 1, elementType: 4, nowCost: 50, form: "9.0");

        var result = new PlayerRecommendationService().RecommendPlayers(MakeBootstrap(defender, forward), NoFixtures, elementTypeId: 2);

        Assert.Equal([1], result.Select(p => p.Id));
    }

    [Fact]
    public void RecommendPlayers_FiltersByMaxCost()
    {
        var cheap = MakePlayer(1, team: 1, elementType: 2, nowCost: 50, form: "1.0");
        var expensive = MakePlayer(2, team: 1, elementType: 2, nowCost: 100, form: "9.0");

        var result = new PlayerRecommendationService().RecommendPlayers(MakeBootstrap(cheap, expensive), NoFixtures, maxCost: 50);

        Assert.Equal([1], result.Select(p => p.Id));
    }

    [Fact]
    public void RecommendPlayers_ExcludesSpecifiedPlayerIds()
    {
        var best = MakePlayer(1, team: 1, elementType: 2, nowCost: 50, form: "9.0");
        var next = MakePlayer(2, team: 1, elementType: 2, nowCost: 50, form: "1.0");

        var result = new PlayerRecommendationService().RecommendPlayers(
            MakeBootstrap(best, next), NoFixtures, excludePlayerIds: new HashSet<int> { 1 });

        Assert.Equal([2], result.Select(p => p.Id));
    }

    [Fact]
    public void RecommendPlayers_LimitsToRequestedCount_KeepingTheHighestScoring()
    {
        var players = Enumerable.Range(1, 5)
            .Select(i => MakePlayer(i, team: 1, elementType: 2, nowCost: 50, form: (i * 1.0).ToString("F1")))
            .ToArray();

        var result = new PlayerRecommendationService().RecommendPlayers(MakeBootstrap(players), NoFixtures, count: 2);

        Assert.Equal([5, 4], result.Select(p => p.Id));
    }

    [Fact]
    public void RecommendPlayers_RanksPlayersWithEasierUpcomingFixturesHigher()
    {
        // Identical players in every respect except which team they're on, and those teams have
        // very different fixture difficulty over the lookahead window.
        var easyFixtureTeam = MakePlayer(1, team: 1, elementType: 2, nowCost: 50, form: "5.0");
        var hardFixtureTeam = MakePlayer(2, team: 2, elementType: 2, nowCost: 50, form: "5.0");

        var fixtures = new List<Fixture>
        {
            new() { Event = 1, Finished = false, TeamH = 1, TeamHDifficulty = 2, TeamA = 9, TeamADifficulty = 2 },
            new() { Event = 1, Finished = false, TeamH = 2, TeamHDifficulty = 5, TeamA = 8, TeamADifficulty = 5 },
        };

        var result = new PlayerRecommendationService().RecommendPlayers(
            MakeBootstrap(easyFixtureTeam, hardFixtureTeam), fixtures, fixtureLookaheadWeeks: 1);

        Assert.Equal([1, 2], result.Select(p => p.Id));
    }
}
