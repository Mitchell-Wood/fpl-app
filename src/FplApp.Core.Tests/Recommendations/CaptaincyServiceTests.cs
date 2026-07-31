using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class CaptaincyServiceTests
{
    private const int EventId = 5;

    private static Player MakePlayer(int id, int team, string form = "0", string pointsPerGame = "0", string status = "a")
        => new()
        {
            Id = id,
            WebName = $"Player{id}",
            Team = team,
            Form = form,
            PointsPerGame = pointsPerGame,
            Status = status,
        };

    private static Pick MakePick(Player player, int position, bool isCaptain = false, bool isViceCaptain = false)
        => new() { Element = player.Id, Position = position, IsCaptain = isCaptain, IsViceCaptain = isViceCaptain };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new()
        {
            Teams = Enumerable.Range(1, 10).Select(id => new Team { Id = id, ShortName = $"T{id}" }).ToList(),
            Elements = players.ToList(),
        };

    [Fact]
    public void SuggestCaptains_ExcludesBenchedAndUnavailablePlayers()
    {
        var starter = MakePlayer(1, team: 1, form: "2.0");
        var benched = MakePlayer(2, team: 1, form: "9.0");
        var injured = MakePlayer(3, team: 1, form: "9.0", status: "i");

        var bootstrap = MakeBootstrap(starter, benched, injured);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1), MakePick(benched, 12), MakePick(injured, 2)] };

        var result = new CaptaincyService().SuggestCaptains(bootstrap, fixtures, picks, EventId);

        Assert.Equal([1], result.Select(r => r.PlayerId));
    }

    [Fact]
    public void SuggestCaptains_FlagsABlankGameweekWithZeroExpectedPoints()
    {
        var player = MakePlayer(1, team: 1, form: "5.0");
        var bootstrap = MakeBootstrap(player);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 2, TeamHDifficulty = 3, TeamA = 3, TeamADifficulty = 3 } }; // team 1 doesn't play
        var picks = new TeamPicks { Picks = [MakePick(player, 1)] };

        var result = new CaptaincyService().SuggestCaptains(bootstrap, fixtures, picks, EventId);

        var suggestion = Assert.Single(result);
        Assert.Equal(0.0, suggestion.ExpectedPoints);
        Assert.Equal("Blank gameweek", suggestion.Note);
        Assert.Empty(suggestion.Fixtures);
    }

    [Fact]
    public void SuggestCaptains_SumsBothFixturesInADoubleGameweek()
    {
        var player = MakePlayer(1, team: 4, form: "3.0");
        var bootstrap = MakeBootstrap(player);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 4, TeamHDifficulty = 2, TeamA = 5, TeamADifficulty = 5 },
            new() { Event = EventId, TeamH = 6, TeamHDifficulty = 6, TeamA = 4, TeamADifficulty = 4 },
        };
        var picks = new TeamPicks { Picks = [MakePick(player, 1)] };

        var result = new CaptaincyService().SuggestCaptains(bootstrap, fixtures, picks, EventId);

        var suggestion = Assert.Single(result);
        // 3.0 * (6-2)/3 [home] + 3.0 * (6-4)/3 [away] = 4.0 + 2.0
        Assert.Equal(6.0, suggestion.ExpectedPoints);
        Assert.Equal("Double gameweek", suggestion.Note);
        Assert.Equal(2, suggestion.Fixtures.Count);
    }

    [Fact]
    public void SuggestCaptains_FallsBackToPointsPerGame_WhenFormIsZero()
    {
        var player = MakePlayer(1, team: 1, form: "0", pointsPerGame: "5.0");
        var bootstrap = MakeBootstrap(player);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 } };
        var picks = new TeamPicks { Picks = [MakePick(player, 1)] };

        var result = new CaptaincyService().SuggestCaptains(bootstrap, fixtures, picks, EventId);

        Assert.Equal(5.0, Assert.Single(result).ExpectedPoints);
    }

    [Fact]
    public void SuggestCaptains_RanksDescending_AndReflectsCurrentCaptainFlags()
    {
        var lowerScorer = MakePlayer(1, team: 1, form: "4.0");
        var higherScorer = MakePlayer(2, team: 2, form: "6.0");
        var bootstrap = MakeBootstrap(lowerScorer, higherScorer);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 },
            new() { Event = EventId, TeamH = 2, TeamHDifficulty = 3, TeamA = 8, TeamADifficulty = 3 },
        };
        var picks = new TeamPicks { Picks = [MakePick(lowerScorer, 1, isCaptain: true), MakePick(higherScorer, 2, isViceCaptain: true)] };

        var result = new CaptaincyService().SuggestCaptains(bootstrap, fixtures, picks, EventId);

        Assert.Equal([2, 1], result.Select(r => r.PlayerId));
        Assert.True(result.First(r => r.PlayerId == 1).IsCurrentCaptain);
        Assert.True(result.First(r => r.PlayerId == 2).IsCurrentViceCaptain);
    }
}
