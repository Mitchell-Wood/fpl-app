using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class FixtureDifficultyCalculatorTests
{
    private static Fixture Fx(int @event, int teamH, int teamHDifficulty, int teamA, int teamADifficulty, bool finished = false) => new()
    {
        Event = @event,
        Finished = finished,
        TeamH = teamH,
        TeamHDifficulty = teamHDifficulty,
        TeamA = teamA,
        TeamADifficulty = teamADifficulty,
    };

    [Fact]
    public void NoFixtures_ReturnsEmpty()
    {
        var result = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam([], lookaheadWeeks: 3);

        Assert.Empty(result);
    }

    [Fact]
    public void AllFixturesFinished_ReturnsEmpty()
    {
        // No unplayed fixture means there's no "next event" to look ahead from.
        var fixtures = new List<Fixture> { Fx(1, teamH: 1, teamHDifficulty: 2, teamA: 2, teamADifficulty: 3, finished: true) };

        var result = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, lookaheadWeeks: 3);

        Assert.Empty(result);
    }

    [Fact]
    public void BlankGameweek_ContributesNothingForThatTeam()
    {
        // Team 1 has no fixture in the window at all (blank gameweek) — it must not appear,
        // rather than appearing with a misleading default difficulty.
        var fixtures = new List<Fixture>
        {
            Fx(1, teamH: 1, teamHDifficulty: 2, teamA: 2, teamADifficulty: 4, finished: true),
            Fx(3, teamH: 2, teamHDifficulty: 3, teamA: 3, teamADifficulty: 5),
        };

        var result = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, lookaheadWeeks: 2);

        Assert.False(result.ContainsKey(1));
    }

    [Fact]
    public void DoubleGameweek_CountsBothFixturesForTheTeam()
    {
        var fixtures = new List<Fixture>
        {
            Fx(2, teamH: 1, teamHDifficulty: 2, teamA: 5, teamADifficulty: 3),
            Fx(2, teamH: 6, teamHDifficulty: 4, teamA: 1, teamADifficulty: 4),
        };

        var result = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, lookaheadWeeks: 1);

        Assert.Equal([2, 4], result[1]);
    }

    [Fact]
    public void LookaheadWindow_ExcludesFixturesOutsideRange()
    {
        var fixtures = new List<Fixture>
        {
            Fx(2, teamH: 1, teamHDifficulty: 2, teamA: 2, teamADifficulty: 2), // in window (next event)
            Fx(3, teamH: 1, teamHDifficulty: 3, teamA: 2, teamADifficulty: 3), // in window (last event, lookahead=2)
            Fx(4, teamH: 1, teamHDifficulty: 5, teamA: 2, teamADifficulty: 5), // outside window
        };

        var result = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, lookaheadWeeks: 2);

        Assert.Equal([2, 3], result[1]);
    }

    [Fact]
    public void FinishedFixturesInThePast_AreIgnoredWhenFindingTheNextEvent()
    {
        var fixtures = new List<Fixture>
        {
            Fx(1, teamH: 1, teamHDifficulty: 5, teamA: 2, teamADifficulty: 5, finished: true),
            Fx(2, teamH: 1, teamHDifficulty: 3, teamA: 2, teamADifficulty: 2),
        };

        var result = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, lookaheadWeeks: 1);

        Assert.Equal([3], result[1]);
        Assert.Equal([2], result[2]);
    }

    [Fact]
    public void AverageUpcomingDifficultyByTeam_AveragesADoubleGameweek()
    {
        var fixtures = new List<Fixture>
        {
            Fx(2, teamH: 1, teamHDifficulty: 2, teamA: 5, teamADifficulty: 4),
            Fx(2, teamH: 6, teamHDifficulty: 4, teamA: 1, teamADifficulty: 6),
        };

        var result = FixtureDifficultyCalculator.AverageUpcomingDifficultyByTeam(fixtures, lookaheadWeeks: 1);

        Assert.Equal(4.0, result[1]);
    }

    [Fact]
    public void RawDifficultiesForEvent_OnlyIncludesTheChosenEvent_RegardlessOfFinishedOrOrder()
    {
        var fixtures = new List<Fixture>
        {
            Fx(1, teamH: 1, teamHDifficulty: 5, teamA: 9, teamADifficulty: 5, finished: true), // different (past) event
            Fx(3, teamH: 1, teamHDifficulty: 4, teamA: 9, teamADifficulty: 4), // different (future) event
            Fx(2, teamH: 1, teamHDifficulty: 2, teamA: 2, teamADifficulty: 3), // the chosen event
        };

        var result = FixtureDifficultyCalculator.RawDifficultiesForEvent(fixtures, eventId: 2);

        Assert.Equal([2], result[1]);
        Assert.Equal([3], result[2]);
    }

    [Fact]
    public void RawDifficultiesForEvent_TeamWithNoFixtureThatWeek_IsAbsent()
    {
        var fixtures = new List<Fixture> { Fx(2, teamH: 1, teamHDifficulty: 3, teamA: 2, teamADifficulty: 3) };

        var result = FixtureDifficultyCalculator.RawDifficultiesForEvent(fixtures, eventId: 2);

        Assert.False(result.ContainsKey(9));
    }

    [Fact]
    public void RawDifficultiesForEvent_DoubleGameweek_CountsBothFixtures()
    {
        var fixtures = new List<Fixture>
        {
            Fx(2, teamH: 1, teamHDifficulty: 2, teamA: 5, teamADifficulty: 3),
            Fx(2, teamH: 6, teamHDifficulty: 4, teamA: 1, teamADifficulty: 4),
        };

        var result = FixtureDifficultyCalculator.RawDifficultiesForEvent(fixtures, eventId: 2);

        Assert.Equal([2, 4], result[1]);
    }
}
