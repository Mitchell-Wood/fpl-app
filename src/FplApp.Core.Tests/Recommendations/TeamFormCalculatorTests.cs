using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class TeamFormCalculatorTests
{
    private const int TeamA = 1;
    private const int TeamB = 2;

    private static Fixture Played(int eventId, int teamH, int teamA, int teamHScore, int teamAScore)
        => new()
        {
            Event = eventId,
            TeamH = teamH,
            TeamA = teamA,
            TeamHScore = teamHScore,
            TeamAScore = teamAScore,
            FinishedProvisional = true,
        };

    [Fact]
    public void ComputeTeamForm_ReturnsEmpty_WhenNoFixturesHaveFinished()
    {
        var fixtures = new List<Fixture>
        {
            new() { Event = 1, TeamH = TeamA, TeamA = TeamB, FinishedProvisional = false },
        };

        var form = TeamFormCalculator.ComputeTeamForm(fixtures);

        Assert.Empty(form);
    }

    [Fact]
    public void ComputeTeamForm_IgnoresFixturesNotYetFinished()
    {
        var fixtures = new List<Fixture>
        {
            new() { Event = 1, TeamH = TeamA, TeamA = TeamB, TeamHScore = 5, TeamAScore = 0, FinishedProvisional = false },
        };

        var form = TeamFormCalculator.ComputeTeamForm(fixtures);

        Assert.Empty(form);
    }

    [Fact]
    public void ComputeTeamForm_GivesAHigherAttackFactor_ToATeamScoringMoreAtHome()
    {
        var fixtures = new List<Fixture>
        {
            Played(1, TeamA, 3, teamHScore: 4, teamAScore: 0),
            Played(2, TeamB, 3, teamHScore: 0, teamAScore: 0),
        };

        var form = TeamFormCalculator.ComputeTeamForm(fixtures);

        Assert.True(form[TeamA].AttackHome > form[TeamB].AttackHome, "a team scoring heavily at home should rate higher than one that hasn't scored");
    }

    [Fact]
    public void ComputeTeamForm_GivesALowerDefenceFactor_ToATeamConcedingMoreAtHome()
    {
        var fixtures = new List<Fixture>
        {
            Played(1, TeamA, 3, teamHScore: 0, teamAScore: 4), // TeamA concedes heavily at home
            Played(2, TeamB, 3, teamHScore: 2, teamAScore: 0), // TeamB keeps a clean sheet at home
        };

        var form = TeamFormCalculator.ComputeTeamForm(fixtures);

        Assert.True(form[TeamB].DefenceHome > form[TeamA].DefenceHome, "a team conceding heavily at home should rate lower defensively than one keeping clean sheets");
    }

    [Fact]
    public void ComputeTeamForm_ShrinksTowardTheNeutralPrior_WithOnlyOneGamePlayed()
    {
        // A single freak scoreline shouldn't swing the factor to an extreme — shrinkage should pull
        // it most of the way back toward the neutral (1.0) baseline.
        var fixtures = new List<Fixture> { Played(1, TeamA, TeamB, teamHScore: 8, teamAScore: 0) };

        var form = TeamFormCalculator.ComputeTeamForm(fixtures);

        // Fully trusting the single 8-0 scoreline would give a factor of 8/1.5 ≈ 5.33 — shrinkage
        // toward the neutral prior should pull it well below that, even though one game still moves
        // it meaningfully above the neutral 1.0 baseline.
        Assert.True(form[TeamA].AttackHome is > 1.0 and < 4.0, $"expected a shrunk-toward-neutral factor, got {form[TeamA].AttackHome}");
    }

    [Fact]
    public void ComputeTeamForm_WeighsRecentGamesOnly_NotTheWholeSeason()
    {
        // An extreme result 7 games ago (older than the rolling window) shouldn't still be dragging
        // the factor around once several ordinary games have followed it.
        var fixtures = new List<Fixture>
        {
            Played(1, TeamA, 99, teamHScore: 10, teamAScore: 0),
            Played(2, TeamA, 99, teamHScore: 1, teamAScore: 1),
            Played(3, TeamA, 99, teamHScore: 1, teamAScore: 1),
            Played(4, TeamA, 99, teamHScore: 1, teamAScore: 1),
            Played(5, TeamA, 99, teamHScore: 1, teamAScore: 1),
            Played(6, TeamA, 99, teamHScore: 1, teamAScore: 1),
            Played(7, TeamA, 99, teamHScore: 1, teamAScore: 1),
        };
        var recentOnly = fixtures.Skip(1).ToList(); // same trailing 6 games, without the old outlier

        var withOutlier = TeamFormCalculator.ComputeTeamForm(fixtures);
        var withoutOutlier = TeamFormCalculator.ComputeTeamForm(recentOnly);

        Assert.Equal(withoutOutlier[TeamA].AttackHome, withOutlier[TeamA].AttackHome, precision: 6);
    }
}
