using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class SquadBuilderServiceTests
{
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

    /// <summary>
    /// A fixture per given team, each against its own unused synthetic opponent, all at difficulty
    /// 3 (average) — gives every real team a fixture-factor of exactly 1, so a player's projected
    /// points collapse to just their form value, keeping the test arithmetic easy to verify by hand.
    /// </summary>
    private static List<Fixture> FixturesFor(IEnumerable<int> teamIds)
        => teamIds.Select(id => new Fixture { Event = 1, Finished = false, TeamH = id, TeamHDifficulty = 3, TeamA = id + 1000, TeamADifficulty = 3 }).ToList();

    private static BootstrapStatic MakeBootstrap(IEnumerable<int> teamIds, params Player[] players)
        => new()
        {
            Teams = teamIds.Select(id => new Team { Id = id, ShortName = $"T{id}" }).ToList(),
            Elements = players.ToList(),
        };

    [Fact]
    public void BuildSquad_ProducesAValidSquad_RespectingQuotasAndBudget()
    {
        // 8 teams, each fielding a uniform GK/2 DEF/2 MID/FWD at the same cost and form — plenty of
        // depth per position and per team, and no upgrade is ever better than another (all tied),
        // so the result is fully determined by the position quotas alone.
        var teamIds = Enumerable.Range(1, 8).ToList();
        var players = new List<Player>();
        foreach (var team in teamIds)
        {
            players.Add(MakePlayer(team * 10 + 1, team, elementType: 1, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 2, team, elementType: 2, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 3, team, elementType: 2, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 4, team, elementType: 3, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 5, team, elementType: 3, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 6, team, elementType: 4, nowCost: 40, form: "1.0"));
        }

        var bootstrap = MakeBootstrap(teamIds, players.ToArray());
        var result = new SquadBuilderService().BuildSquad(bootstrap, FixturesFor(teamIds), budget: 1000);

        Assert.Equal(15, result.Players.Count);
        Assert.Equal(2, result.Players.Count(p => p.ElementType == 1));
        Assert.Equal(5, result.Players.Count(p => p.ElementType == 2));
        Assert.Equal(5, result.Players.Count(p => p.ElementType == 3));
        Assert.Equal(3, result.Players.Count(p => p.ElementType == 4));
        Assert.Equal(600, result.TotalCost); // 15 players x 40
        Assert.Equal(400, result.BudgetRemaining);
        Assert.True(result.TotalCost <= result.Budget);
        Assert.All(teamIds, team => Assert.True(result.Players.Count(p => p.TeamName == $"T{team}") <= 3));
    }

    [Fact]
    public void BuildSquad_ThrowsWhenBudgetIsBelowTheCheapestLegalSquad()
    {
        var teamIds = Enumerable.Range(1, 8).ToList();
        var players = new List<Player>();
        foreach (var team in teamIds)
        {
            players.Add(MakePlayer(team * 10 + 1, team, elementType: 1, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 2, team, elementType: 2, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 3, team, elementType: 3, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 4, team, elementType: 4, nowCost: 40, form: "1.0"));
        }

        var bootstrap = MakeBootstrap(teamIds, players.ToArray());

        Assert.Throws<InvalidOperationException>(() => new SquadBuilderService().BuildSquad(bootstrap, FixturesFor(teamIds), budget: 100));
    }

    [Fact]
    public void BuildSquad_UpgradesTowardBetterPlayers_ButNeverExceedsThePerTeamCap()
    {
        // Team 1 has 6 excellent (but pricier) defenders — only 3 can legally be selected. Teams
        // 2-5 have cheap, weaker defenders that fill the floor squad and the remaining 2 DEF slots
        // once team 1's cap is reached. Other positions use a single uniform tier spread across
        // several teams so they can't produce any competing upgrade.
        var players = new List<Player>();
        for (var i = 0; i < 6; i++)
        {
            players.Add(MakePlayer(100 + i, team: 1, elementType: 2, nowCost: 60, form: "9.0"));
        }
        foreach (var team in new[] { 2, 3, 4, 5 })
        {
            players.Add(MakePlayer(team * 10 + 1, team, elementType: 2, nowCost: 40, form: "2.0"));
            players.Add(MakePlayer(team * 10 + 2, team, elementType: 2, nowCost: 40, form: "2.0"));
        }
        // GK and MID use entirely separate teams from each other (and from the DEF teams above) so
        // the per-team cap can't accidentally interact across positions — this test is only about
        // the DEF cap.
        foreach (var team in new[] { 6, 7 })
        {
            players.Add(MakePlayer(team * 10 + 1, team, elementType: 1, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 2, team, elementType: 1, nowCost: 40, form: "1.0"));
        }
        foreach (var team in new[] { 9, 10, 11 })
        {
            players.Add(MakePlayer(team * 10 + 1, team, elementType: 3, nowCost: 40, form: "1.0"));
            players.Add(MakePlayer(team * 10 + 2, team, elementType: 3, nowCost: 40, form: "1.0"));
        }
        players.Add(MakePlayer(801, team: 8, elementType: 4, nowCost: 40, form: "1.0"));
        players.Add(MakePlayer(802, team: 8, elementType: 4, nowCost: 40, form: "1.0"));
        players.Add(MakePlayer(803, team: 8, elementType: 4, nowCost: 40, form: "1.0"));

        var teamIds = Enumerable.Range(1, 11).ToList();
        var bootstrap = MakeBootstrap(teamIds, players.ToArray());
        var result = new SquadBuilderService().BuildSquad(bootstrap, FixturesFor(teamIds), budget: 1000);

        var team1DefCount = result.Players.Count(p => p.TeamName == "T1" && p.ElementType == 2);
        Assert.Equal(3, team1DefCount); // capped at 3, despite 6 being available and affordable
        Assert.All(result.Players.Where(p => p.TeamName == "T1"), p => Assert.Equal(9.0, p.ProjectedPoints));
    }

    [Fact]
    public void BuildSquad_PicksTheHighestScoringValidFormation_AndCaptainsTheTopScorer()
    {
        // Budget set exactly to the floor cost of this pool, so no upgrade swaps occur and the
        // selected 15 (and therefore the formation/captain math) is fully deterministic.
        var teamIds = Enumerable.Range(1, 15).ToList();
        var players = new List<Player>
        {
            MakePlayer(1, team: 1, elementType: 1, nowCost: 40, form: "1.0"),
            MakePlayer(2, team: 2, elementType: 1, nowCost: 40, form: "1.0"),
        };
        for (var i = 0; i < 5; i++)
        {
            players.Add(MakePlayer(10 + i, team: 3 + i, elementType: 2, nowCost: 40, form: "1.0"));
        }
        for (var i = 0; i < 5; i++)
        {
            players.Add(MakePlayer(20 + i, team: 8 + i, elementType: 3, nowCost: 40, form: "5.0"));
        }
        players.Add(MakePlayer(30, team: 13, elementType: 4, nowCost: 40, form: "9.0"));
        players.Add(MakePlayer(31, team: 14, elementType: 4, nowCost: 40, form: "8.5"));
        players.Add(MakePlayer(32, team: 15, elementType: 4, nowCost: 40, form: "8.0"));

        var bootstrap = MakeBootstrap(teamIds, players.ToArray());
        var result = new SquadBuilderService().BuildSquad(bootstrap, FixturesFor(teamIds), budget: 600);

        Assert.Equal(0, result.BudgetRemaining);
        Assert.Equal("3-4-3", result.Formation);
        Assert.Equal(11, result.Players.Count(p => p.IsStarting));
        Assert.Equal(1, result.Players.Count(p => p.IsStarting && p.ElementType == 1));
        Assert.Equal(3, result.Players.Count(p => p.IsStarting && p.ElementType == 2));
        Assert.Equal(4, result.Players.Count(p => p.IsStarting && p.ElementType == 3));
        Assert.Equal(3, result.Players.Count(p => p.IsStarting && p.ElementType == 4));

        var captain = Assert.Single(result.Players, p => p.IsCaptain);
        Assert.Equal(30, captain.PlayerId);
        Assert.True(captain.IsStarting);
    }

    [Fact]
    public void BuildSquad_ThrowsOnNullArguments()
    {
        var service = new SquadBuilderService();
        Assert.Throws<ArgumentNullException>(() => service.BuildSquad(null!, [], budget: 1000));
        Assert.Throws<ArgumentNullException>(() => service.BuildSquad(new BootstrapStatic(), null!, budget: 1000));
    }
}
