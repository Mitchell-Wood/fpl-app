using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class FixturesRemainingCalculatorTests
{
    private const int EventId = 5;

    private static Player MakePlayer(int id, int team)
        => new() { Id = id, WebName = $"Player{id}", Team = team };

    private static Pick MakePick(Player player, int position)
        => new() { Element = player.Id, Position = position };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new()
        {
            Teams = Enumerable.Range(1, 10).Select(id => new Team { Id = id, ShortName = $"T{id}" }).ToList(),
            Elements = players.ToList(),
        };

    [Fact]
    public void CountRemaining_CountsAnUnplayedFixtureForAStarter()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountRemaining_IgnoresBenchedPlayers()
    {
        var benched = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(benched);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(benched, 12)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountRemaining_ExcludesAFinishedFixture()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = true } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountRemaining_TreatsAFixtureAsPlayed_AssoonAsFinishedProvisional_EvenIfFinishedIsStillFalse()
    {
        // FPL leaves "finished" false for a while post-match pending official confirmation (bonus
        // points etc.), while "finished_provisional" flips true immediately at full-time — this is
        // the real shape the live API returns for a just-ended match.
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, Finished = false, FinishedProvisional = true } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountRemaining_CountsBothLegsOfADoubleGameweek()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false },
            new() { Event = EventId, TeamH = 3, TeamA = 1, FinishedProvisional = false },
        };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Equal(2, result);
    }

    [Fact]
    public void CountRemaining_IsZeroForABlankGameweek()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 2, TeamA = 3, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountRemaining_SumsAcrossMultipleStarters()
    {
        var starterOne = MakePlayer(1, team: 1);
        var starterTwo = MakePlayer(2, team: 3);
        var bootstrap = MakeBootstrap(starterOne, starterTwo);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false },
            new() { Event = EventId, TeamH = 3, TeamA = 4, FinishedProvisional = false },
        };
        var picks = new TeamPicks { Picks = [MakePick(starterOne, 1), MakePick(starterTwo, 2)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Equal(2, result);
    }
}
