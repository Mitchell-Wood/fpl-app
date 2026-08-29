using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class FixturesRemainingCalculatorTests
{
    private const int EventId = 5;
    private const int Gk = 1;
    private const int Def = 2;
    private const int Mid = 3;
    private const int Fwd = 4;

    private static readonly Dictionary<int, int> NoMinutes = new();

    private static Player MakePlayer(int id, int team, int elementType = Def)
        => new() { Id = id, WebName = $"Player{id}", Team = team, ElementType = elementType };

    private static Pick MakePick(Player player, int position, bool isCaptain = false)
        => new() { Element = player.Id, Position = position, IsCaptain = isCaptain };

    private static BootstrapStatic MakeBootstrap(params Player[] players)
        => new()
        {
            Teams = Enumerable.Range(1, 40).Select(id => new Team { Id = id, ShortName = $"T{id}" }).ToList(),
            Elements = players.ToList(),
        };

    [Fact]
    public void CountRemaining_CountsAnUnplayedFixtureForAStarter()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountRemaining_IgnoresBenchedPlayers()
    {
        var benched = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(benched);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(benched, 12)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountRemaining_IncludesBenchedPlayers_WhenBenchBoostIsActive()
    {
        var starter = MakePlayer(1, team: 1);
        var benched = MakePlayer(2, team: 3);
        var bootstrap = MakeBootstrap(starter, benched);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false },
            new() { Event = EventId, TeamH = 3, TeamA = 4, FinishedProvisional = false },
        };
        var picks = new TeamPicks
        {
            ActiveChip = "bboost",
            Picks = [MakePick(starter, 1), MakePick(benched, 12)],
        };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

        Assert.Equal(2, result);
    }

    [Fact]
    public void CountRemaining_ExcludesBenchedPlayers_WhenAnotherChipIsActive()
    {
        var starter = MakePlayer(1, team: 1);
        var benched = MakePlayer(2, team: 3);
        var bootstrap = MakeBootstrap(starter, benched);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false },
            new() { Event = EventId, TeamH = 3, TeamA = 4, FinishedProvisional = false },
        };
        var picks = new TeamPicks
        {
            ActiveChip = "3xc",
            Picks = [MakePick(starter, 1), MakePick(benched, 12)],
        };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountRemaining_ExcludesAFinishedFixture()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = true } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

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

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

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

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

        Assert.Equal(2, result);
    }

    [Fact]
    public void CountRemaining_IsZeroForABlankGameweek()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 2, TeamA = 3, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

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

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, NoMinutes);

        Assert.Equal(2, result);
    }

    // ---- Auto-substitution ----

    [Fact]
    public void CountRemaining_DoesNotAutoSub_WhileAStarterMightStillComeOn()
    {
        // Fixture in progress (not yet finished_provisional) with 0 minutes so far — the player
        // could still be brought on, so FPL won't auto-sub them yet.
        var starter = MakePlayer(1, team: 1);
        var reserveGk = MakePlayer(2, team: 5, elementType: Gk);
        var bootstrap = MakeBootstrap(starter, reserveGk);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1), MakePick(reserveGk, 12)] };
        var minutes = new Dictionary<int, int> { [starter.Id] = 0 };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, minutes);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountRemaining_AutoSubsAConfirmedOutGoalkeeper_ForTheReserveGoalkeeper()
    {
        var starterGk = MakePlayer(1, team: 1, elementType: Gk);
        var reserveGk = MakePlayer(2, team: 2, elementType: Gk);
        var bootstrap = MakeBootstrap(starterGk, reserveGk);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamA = 9, FinishedProvisional = true }, // starter GK's game: finished, he didn't play
            new() { Event = EventId, TeamH = 2, TeamA = 8, FinishedProvisional = false }, // reserve GK's game: still to play
        };
        var picks = new TeamPicks { Picks = [MakePick(starterGk, 1), MakePick(reserveGk, 12)] };
        var minutes = new Dictionary<int, int> { [starterGk.Id] = 0 };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, minutes);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountRemaining_AutoSubsAConfirmedOutOutfielder_WhenFormationStaysLegal()
    {
        // 1 GK, 4 DEF, 4 MID, 2 FWD starting XI. One DEF is confirmed out; the reserve MID can
        // legally replace him (DEF drops to 3, still >= the minimum of 3).
        var gk = MakePlayer(1, team: 1, Gk);
        var defs = new[] { MakePlayer(2, 2, Def), MakePlayer(3, 3, Def), MakePlayer(4, 4, Def), MakePlayer(5, 5, Def) };
        var mids = new[] { MakePlayer(6, 6, Mid), MakePlayer(7, 7, Mid), MakePlayer(8, 8, Mid), MakePlayer(9, 9, Mid) };
        var fwds = new[] { MakePlayer(10, 10, Fwd), MakePlayer(11, 11, Fwd) };
        var reserveMid = MakePlayer(12, 12, Mid);

        var bootstrap = MakeBootstrap([gk, .. defs, .. mids, .. fwds, reserveMid]);

        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = defs[0].Team, TeamA = 20, FinishedProvisional = true }, // confirmed-out DEF's game
            new() { Event = EventId, TeamH = reserveMid.Team, TeamA = 21, FinishedProvisional = false }, // reserve's game still to play
        };

        var picks = new TeamPicks
        {
            Picks =
            [
                MakePick(gk, 1),
                MakePick(defs[0], 2), MakePick(defs[1], 3), MakePick(defs[2], 4), MakePick(defs[3], 5),
                MakePick(mids[0], 6), MakePick(mids[1], 7), MakePick(mids[2], 8), MakePick(mids[3], 9),
                MakePick(fwds[0], 10), MakePick(fwds[1], 11),
                MakePick(reserveMid, 13),
            ],
        };
        // Everyone but the target DEF has already played, so only he is eligible to be subbed off.
        var minutes = new[] { gk }.Concat(defs).Concat(mids).Concat(fwds).ToDictionary(p => p.Id, _ => 90);
        minutes[defs[0].Id] = 0;

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, minutes);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountRemaining_SkipsAnIneligibleReserve_ButUsesTheNextOneThatKeepsFormationLegal()
    {
        // 1 GK, 3 DEF (the minimum), 4 MID, 3 FWD. One DEF is confirmed out. The first reserve
        // (a MID) would drop DEF below the minimum of 3, so it's skipped; the second reserve (a
        // DEF) keeps the formation legal, so it's used instead.
        var gk = MakePlayer(1, team: 1, Gk);
        var defs = new[] { MakePlayer(2, 2, Def), MakePlayer(3, 3, Def), MakePlayer(4, 4, Def) };
        var mids = new[] { MakePlayer(5, 5, Mid), MakePlayer(6, 6, Mid), MakePlayer(7, 7, Mid), MakePlayer(8, 8, Mid) };
        var fwds = new[] { MakePlayer(9, 9, Fwd), MakePlayer(10, 10, Fwd), MakePlayer(11, 11, Fwd) };
        var reserveMid = MakePlayer(12, 12, Mid);
        var reserveDef = MakePlayer(13, 13, Def);

        var bootstrap = MakeBootstrap([gk, .. defs, .. mids, .. fwds, reserveMid, reserveDef]);

        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = defs[0].Team, TeamA = 20, FinishedProvisional = true },
            new() { Event = EventId, TeamH = reserveMid.Team, TeamA = 21, FinishedProvisional = false },
            new() { Event = EventId, TeamH = reserveDef.Team, TeamA = 22, FinishedProvisional = false },
        };

        var picks = new TeamPicks
        {
            Picks =
            [
                MakePick(gk, 1),
                MakePick(defs[0], 2), MakePick(defs[1], 3), MakePick(defs[2], 4),
                MakePick(mids[0], 5), MakePick(mids[1], 6), MakePick(mids[2], 7), MakePick(mids[3], 8),
                MakePick(fwds[0], 9), MakePick(fwds[1], 10), MakePick(fwds[2], 11),
                MakePick(reserveMid, 13), MakePick(reserveDef, 14),
            ],
        };
        // Everyone but the target DEF has already played, so only he is eligible to be subbed off.
        var minutes = new[] { gk }.Concat(defs).Concat(mids).Concat(fwds).ToDictionary(p => p.Id, _ => 90);
        minutes[defs[0].Id] = 0;

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, minutes);

        // Only reserveDef's fixture should count (1) — reserveMid never comes on, so its fixture
        // must not be counted (which would give 2).
        Assert.Equal(1, result);
    }

    [Fact]
    public void CountRemaining_DoesNotAutoSub_WhenNoLegalReserveIsAvailable()
    {
        // 1 GK, 3 DEF (the minimum). The confirmed-out DEF has no eligible reserve (the only bench
        // outfield player is a MID, which would break the formation), so he stays put with 0 left.
        var gk = MakePlayer(1, team: 1, Gk);
        var defs = new[] { MakePlayer(2, 2, Def), MakePlayer(3, 3, Def), MakePlayer(4, 4, Def) };
        var mids = new[] { MakePlayer(5, 5, Mid), MakePlayer(6, 6, Mid), MakePlayer(7, 7, Mid), MakePlayer(8, 8, Mid) };
        var fwds = new[] { MakePlayer(9, 9, Fwd), MakePlayer(10, 10, Fwd), MakePlayer(11, 11, Fwd) };
        var reserveMid = MakePlayer(12, 12, Mid);

        var bootstrap = MakeBootstrap([gk, .. defs, .. mids, .. fwds, reserveMid]);

        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = defs[0].Team, TeamA = 20, FinishedProvisional = true },
            new() { Event = EventId, TeamH = reserveMid.Team, TeamA = 21, FinishedProvisional = false },
        };

        var picks = new TeamPicks
        {
            Picks =
            [
                MakePick(gk, 1),
                MakePick(defs[0], 2), MakePick(defs[1], 3), MakePick(defs[2], 4),
                MakePick(mids[0], 5), MakePick(mids[1], 6), MakePick(mids[2], 7), MakePick(mids[3], 8),
                MakePick(fwds[0], 9), MakePick(fwds[1], 10), MakePick(fwds[2], 11),
                MakePick(reserveMid, 13),
            ],
        };
        // Everyone but the target DEF has already played, so only he is eligible to be subbed off.
        var minutes = new[] { gk }.Concat(defs).Concat(mids).Concat(fwds).ToDictionary(p => p.Id, _ => 90);
        minutes[defs[0].Id] = 0;

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, minutes);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountRemaining_DoesNotAutoSub_WhenOnlyOneLegOfADoubleGameweekHasFinished()
    {
        var starter = MakePlayer(1, team: 1);
        var reserve = MakePlayer(2, team: 5);
        var bootstrap = MakeBootstrap(starter, reserve);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamA = 9, FinishedProvisional = true }, // leg 1: finished, 0 minutes
            new() { Event = EventId, TeamH = 8, TeamA = 1, FinishedProvisional = false }, // leg 2: still to play
            new() { Event = EventId, TeamH = reserve.Team, TeamA = 20, FinishedProvisional = false },
        };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1), MakePick(reserve, 13)] };
        var minutes = new Dictionary<int, int> { [starter.Id] = 0 };

        var result = FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, picks, EventId, minutes);

        // Not confirmed out yet (leg 2 still to come), so no sub — just his own remaining leg counts.
        Assert.Equal(1, result);
    }

    // ---- CaptainHasFixtureRemaining ----

    [Fact]
    public void CaptainHasFixtureRemaining_TrueWhenCaptainsFixtureHasNotFinished()
    {
        var captain = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(captain);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(captain, 1, isCaptain: true)] };

        var result = FixturesRemainingCalculator.CaptainHasFixtureRemaining(bootstrap, fixtures, picks, EventId);

        Assert.True(result);
    }

    [Fact]
    public void CaptainHasFixtureRemaining_FalseWhenCaptainsFixtureHasFinished()
    {
        var captain = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(captain);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = true } };
        var picks = new TeamPicks { Picks = [MakePick(captain, 1, isCaptain: true)] };

        var result = FixturesRemainingCalculator.CaptainHasFixtureRemaining(bootstrap, fixtures, picks, EventId);

        Assert.False(result);
    }

    [Fact]
    public void CaptainHasFixtureRemaining_FalseOnABlankGameweekForTheCaptainsTeam()
    {
        var captain = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(captain);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 2, TeamA = 3, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(captain, 1, isCaptain: true)] };

        var result = FixturesRemainingCalculator.CaptainHasFixtureRemaining(bootstrap, fixtures, picks, EventId);

        Assert.False(result);
    }

    [Fact]
    public void CaptainHasFixtureRemaining_TrueWhenOnlyOneLegOfADoubleGameweekHasFinished()
    {
        var captain = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(captain);
        var fixtures = new List<Fixture>
        {
            new() { Event = EventId, TeamH = 1, TeamA = 9, FinishedProvisional = true },
            new() { Event = EventId, TeamH = 8, TeamA = 1, FinishedProvisional = false },
        };
        var picks = new TeamPicks { Picks = [MakePick(captain, 1, isCaptain: true)] };

        var result = FixturesRemainingCalculator.CaptainHasFixtureRemaining(bootstrap, fixtures, picks, EventId);

        Assert.True(result);
    }

    [Fact]
    public void CaptainHasFixtureRemaining_NullWhenNoCaptainPickIsFound()
    {
        var starter = MakePlayer(1, team: 1);
        var bootstrap = MakeBootstrap(starter);
        var fixtures = new List<Fixture> { new() { Event = EventId, TeamH = 1, TeamA = 2, FinishedProvisional = false } };
        var picks = new TeamPicks { Picks = [MakePick(starter, 1)] };

        var result = FixturesRemainingCalculator.CaptainHasFixtureRemaining(bootstrap, fixtures, picks, EventId);

        Assert.Null(result);
    }
}
