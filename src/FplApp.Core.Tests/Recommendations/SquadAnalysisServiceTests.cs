using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class SquadAnalysisServiceTests
{
    private static Player MakePlayer(
        int id, int team = 1, string status = "a", string news = "", int? chanceOfPlaying = null, string form = "0")
        => new()
        {
            Id = id,
            WebName = $"Player{id}",
            Team = team,
            Status = status,
            News = news,
            ChanceOfPlayingNextRound = chanceOfPlaying,
            Form = form,
        };

    private static Pick MakePick(Player player, int position, bool isCaptain = false, bool isViceCaptain = false, int multiplier = 1)
        => new() { Element = player.Id, Position = position, IsCaptain = isCaptain, IsViceCaptain = isViceCaptain, Multiplier = multiplier };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new()
        {
            Teams = Enumerable.Range(1, 10).Select(id => new Team { Id = id, ShortName = $"T{id}" }).ToList(),
            Elements = players.ToList(),
        };

    private static readonly IReadOnlyList<Fixture> UnstartedSeasonFixtures =
        [new() { Event = 1, Finished = false, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 }];

    [Fact]
    public void AnalyzeSquad_FlagsInjuredPlayer_WithNewsSuffix_AndDoesNotAlsoFlagDoubtfulChance()
    {
        var player = MakePlayer(1, status: "i", news: "Hamstring injury", chanceOfPlaying: 25);
        var bootstrap = MakeBootstrap(player);
        var picks = new TeamPicks { Picks = [MakePick(player, 1)] };

        var result = new SquadAnalysisService().AnalyzeSquad(bootstrap, UnstartedSeasonFixtures, picks);

        Assert.Equal(["Injured — Hamstring injury"], Assert.Single(result).Flags);
    }

    [Fact]
    public void AnalyzeSquad_FlagsDoubtfulChanceOfPlaying_OnlyWhenAvailableAndBelowFullChance()
    {
        var doubtful = MakePlayer(1, status: "a", chanceOfPlaying: 75);
        var certain = MakePlayer(2, status: "a", chanceOfPlaying: 100);
        var bootstrap = MakeBootstrap(doubtful, certain);
        var picks = new TeamPicks { Picks = [MakePick(doubtful, 1), MakePick(certain, 2)] };

        var result = new SquadAnalysisService().AnalyzeSquad(bootstrap, UnstartedSeasonFixtures, picks);

        Assert.Equal(["Doubtful (75% chance of playing)"], result.Single(r => r.PlayerId == 1).Flags);
        Assert.Empty(result.Single(r => r.PlayerId == 2).Flags);
    }

    [Fact]
    public void AnalyzeSquad_DoesNotFlagPoorForm_BeforeTheSeasonHasStarted()
    {
        var player = MakePlayer(1, form: "0.5"); // low form, but no fixture has finished yet
        var bootstrap = MakeBootstrap(player);
        var picks = new TeamPicks { Picks = [MakePick(player, 1)] };

        var result = new SquadAnalysisService().AnalyzeSquad(bootstrap, UnstartedSeasonFixtures, picks);

        Assert.DoesNotContain("Poor recent form", Assert.Single(result).Flags);
    }

    [Fact]
    public void AnalyzeSquad_FlagsPoorForm_OnceTheSeasonHasStarted()
    {
        var player = MakePlayer(1, form: "0.5");
        var bootstrap = MakeBootstrap(player);
        var fixtures = new List<Fixture>
        {
            new() { Event = 1, Finished = true, TeamH = 5, TeamHDifficulty = 3, TeamA = 6, TeamADifficulty = 3 },
            new() { Event = 2, Finished = false, TeamH = 1, TeamHDifficulty = 3, TeamA = 9, TeamADifficulty = 3 },
        };
        var picks = new TeamPicks { Picks = [MakePick(player, 1)] };

        var result = new SquadAnalysisService().AnalyzeSquad(bootstrap, fixtures, picks);

        Assert.Contains("Poor recent form", Assert.Single(result).Flags);
    }

    [Fact]
    public void AnalyzeSquad_FlagsToughFixturesAhead_OnlyAboveTheDifficultyThreshold()
    {
        var toughFixtures = MakePlayer(1, team: 1);
        var easyFixtures = MakePlayer(2, team: 2);
        var bootstrap = MakeBootstrap(toughFixtures, easyFixtures);
        var fixtures = new List<Fixture>
        {
            new() { Event = 1, Finished = false, TeamH = 1, TeamHDifficulty = 4, TeamA = 9, TeamADifficulty = 3 }, // avg 4 > 3.5
            new() { Event = 1, Finished = false, TeamH = 2, TeamHDifficulty = 3, TeamA = 8, TeamADifficulty = 3 }, // avg 3, not tough
        };
        var picks = new TeamPicks { Picks = [MakePick(toughFixtures, 1), MakePick(easyFixtures, 2)] };

        var result = new SquadAnalysisService().AnalyzeSquad(bootstrap, fixtures, picks);

        Assert.Contains("Tough fixtures ahead", result.Single(r => r.PlayerId == 1).Flags);
        Assert.DoesNotContain("Tough fixtures ahead", result.Single(r => r.PlayerId == 2).Flags);
    }

    [Fact]
    public void AnalyzeSquad_MarksBenchedPlayers_AndOrdersByPickPosition()
    {
        var starter = MakePlayer(1);
        var benched = MakePlayer(2);
        var bootstrap = MakeBootstrap(starter, benched);
        // Fed in reverse order to prove the service sorts by position itself.
        var picks = new TeamPicks { Picks = [MakePick(benched, 12, multiplier: 0), MakePick(starter, 1, multiplier: 1)] };

        var result = new SquadAnalysisService().AnalyzeSquad(bootstrap, UnstartedSeasonFixtures, picks);

        Assert.Equal([1, 2], result.Select(r => r.PlayerId));
        Assert.False(result[0].IsBenched);
        Assert.True(result[1].IsBenched);
        Assert.Equal(0, result[1].Multiplier);
    }

    [Fact]
    public void AnalyzeSquad_SkipsPicksWhosePlayerIsNotFound()
    {
        var bootstrap = MakeBootstrap(); // no players at all
        var picks = new TeamPicks { Picks = [new Pick { Element = 999, Position = 1 }] };

        var result = new SquadAnalysisService().AnalyzeSquad(bootstrap, UnstartedSeasonFixtures, picks);

        Assert.Empty(result);
    }
}
