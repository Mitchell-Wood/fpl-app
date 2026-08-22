using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class LineupOptimizerServiceTests
{
    private const int EventId = 5;
    private const int Gk = 1;
    private const int Def = 2;
    private const int Mid = 3;
    private const int Fwd = 4;

    private static Player MakePlayer(int id, int team, int elementType, string form = "0", string status = "a")
        => new() { Id = id, WebName = $"Player{id}", Team = team, ElementType = elementType, Form = form, Status = status };

    private static Pick MakePick(Player player, int position)
        => new() { Element = player.Id, Position = position };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new()
        {
            Teams = Enumerable.Range(1, 40).Select(id => new Team { Id = id, ShortName = $"T{id}" }).ToList(),
            Elements = players.ToList(),
        };

    private static Fixture MakeFixture(int team, int opponent, int difficulty = 3)
        => new() { Event = EventId, TeamH = team, TeamA = opponent, TeamHDifficulty = difficulty, TeamADifficulty = difficulty };

    // A standard 2 GK / 5 DEF / 5 MID / 3 FWD squad, ids 1-15 in that order, each on their own team
    // (so fixtures can target individual players), with a given form for each.
    private static (BootstrapStatic Bootstrap, TeamPicks Picks, List<Player> Players) MakeStandardSquad(params string[] forms)
    {
        var shapes = new[] { Gk, Gk, Def, Def, Def, Def, Def, Mid, Mid, Mid, Mid, Mid, Fwd, Fwd, Fwd };
        var players = new List<Player>();
        for (var i = 0; i < shapes.Length; i++)
        {
            players.Add(MakePlayer(i + 1, team: i + 1, elementType: shapes[i], form: forms.Length > i ? forms[i] : "1"));
        }

        var bootstrap = MakeBootstrap([.. players]);
        var picks = new TeamPicks { Picks = players.Select((p, i) => MakePick(p, i + 1)).ToList() };
        return (bootstrap, picks, players);
    }

    [Fact]
    public void OptimizeLineup_StartsTheHigherScoringPlayer_OverALowerScoringBenchedOne()
    {
        // Two goalkeepers in an otherwise-standard squad: whichever one has better form for this
        // gameweek should start, regardless of who FPL had declared as the starter beforehand.
        var forms = new[] { "1.0", "9.0", "3", "3", "3", "3", "1", "3", "3", "3", "3", "1", "3", "3", "1" };
        var (bootstrap, picks, players) = MakeStandardSquad(forms);
        var fixtures = players.Select(p => MakeFixture(p.Team, p.Team + 100)).ToList();

        var result = new LineupOptimizerService().OptimizeLineup(bootstrap, fixtures, picks, EventId);

        Assert.False(result.ByPlayerId[players[0].Id].IsStarting);
        Assert.True(result.ByPlayerId[players[1].Id].IsStarting);
    }

    [Fact]
    public void OptimizeLineup_NeverStartsTwoGoalkeepers()
    {
        var (bootstrap, picks, players) = MakeStandardSquad();
        var fixtures = players.Select(p => MakeFixture(p.Team, p.Team + 100)).ToList();

        var result = new LineupOptimizerService().OptimizeLineup(bootstrap, fixtures, picks, EventId);

        var startingGks = players.Where(p => p.ElementType == Gk && result.ByPlayerId[p.Id].IsStarting);
        Assert.Single(startingGks);
    }

    [Fact]
    public void OptimizeLineup_PicksTheFormationThatMaximizesTotalExpectedPoints()
    {
        // 5th defender scores far better than the 2nd midfielder, so the optimizer should prefer a
        // 5-4-1-shaped XI (well, 5 DEF/4 MID/1 FWD isn't legal — use 5 DEF/2 MID/3 FWD) over 3-4-3
        // whenever that scores higher overall. Simplify: make all 5 DEF strong and only 2 MID strong,
        // so a 5-2-3 (defCount5, midCount2, fwdCount3) formation should win over needing weak mids.
        var forms = new[]
        {
            "1", "1", // GKs
            "9", "9", "9", "9", "9", // DEF all strong
            "9", "9", "1", "1", "1", // only first 2 MID strong
            "9", "9", "9", // FWD all strong
        };
        var (bootstrap, picks, players) = MakeStandardSquad(forms);
        var fixtures = players.Select(p => MakeFixture(p.Team, p.Team + 100)).ToList();

        var result = new LineupOptimizerService().OptimizeLineup(bootstrap, fixtures, picks, EventId);

        Assert.Equal("5-2-3", result.Formation);
    }

    [Fact]
    public void OptimizeLineup_PrefersAFitBenchPlayer_OverAnInjuredStarter_WhenFormationStaysLegal()
    {
        var forms = new[]
        {
            "5", "1", // GKs
            "5", "5", "5", "5", "1", // 5th DEF will be injured
            "5", "5", "5", "5", "1",
            "5", "5", "1",
        };
        var (bootstrap, picks, players) = MakeStandardSquad(forms);
        // Injure the weakest DEF (id 7) — a strong bench alternative doesn't exist in this shape,
        // so instead injure a MID (id 12, form "1") while a same-position bench swap isn't possible
        // since only 5 MIDs exist and 2 must start... simplify: injure DEF #5 (id 7) directly.
        players[6].Status = "i"; // 0-indexed: DEF block is players[2..6], so index 6 = 7th player = last DEF

        var fixtures = players.Select(p => MakeFixture(p.Team, p.Team + 100)).ToList();

        var result = new LineupOptimizerService().OptimizeLineup(bootstrap, fixtures, picks, EventId);

        Assert.False(result.ByPlayerId[players[6].Id].IsStarting);
    }

    [Fact]
    public void OptimizeLineup_PicksCaptainAndViceCaptain_AsTopTwoScorersInTheStartingXi()
    {
        var forms = new[]
        {
            "1", "1",
            "3", "3", "3", "3", "1",
            "10", "9", "3", "3", "1", // top scorer and 2nd-top scorer are MIDs
            "3", "3", "1",
        };
        var (bootstrap, picks, players) = MakeStandardSquad(forms);
        var fixtures = players.Select(p => MakeFixture(p.Team, p.Team + 100)).ToList();

        var result = new LineupOptimizerService().OptimizeLineup(bootstrap, fixtures, picks, EventId);

        var topScorer = players[7]; // form "10"
        var secondScorer = players[8]; // form "9"
        Assert.True(result.ByPlayerId[topScorer.Id].IsCaptain);
        Assert.True(result.ByPlayerId[secondScorer.Id].IsViceCaptain);
    }

    [Fact]
    public void OptimizeLineup_ReportsExpectedPointsForEveryPlayer_StartingOrNot()
    {
        var weakStarter = MakePlayer(1, team: 1, Gk, form: "1.0");
        var strongBench = MakePlayer(2, team: 2, Gk, form: "9.0");
        var bootstrap = MakeBootstrap(weakStarter, strongBench);
        var fixtures = new List<Fixture> { MakeFixture(1, 20, difficulty: 3), MakeFixture(2, 21, difficulty: 3) };
        var picks = new TeamPicks { Picks = [MakePick(weakStarter, 1), MakePick(strongBench, 12)] };

        var result = new LineupOptimizerService().OptimizeLineup(bootstrap, fixtures, picks, EventId);

        // form 1.0 * ((6-3)/3) = 1.0; form 9.0 * 1.0 = 9.0
        Assert.Equal(1.0, result.ByPlayerId[weakStarter.Id].ExpectedPoints);
        Assert.Equal(9.0, result.ByPlayerId[strongBench.Id].ExpectedPoints);
    }
}
