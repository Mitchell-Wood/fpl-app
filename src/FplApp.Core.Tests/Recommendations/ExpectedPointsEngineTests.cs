using FplApp.Core.Models;
using FplApp.Core.Recommendations;

namespace FplApp.Core.Tests.Recommendations;

public class ExpectedPointsEngineTests
{
    private static Player MakePlayer(
        int elementType = 3,
        string form = "0",
        string pointsPerGame = "0",
        string epNext = "0",
        int minutes = 0,
        string xgi = "0",
        string xgc = "0",
        double defensiveContributionPer90 = 0,
        int? penaltiesOrder = null,
        int? freekicksOrder = null,
        int? cornersOrder = null)
        => new()
        {
            Id = 1,
            ElementType = elementType,
            Form = form,
            PointsPerGame = pointsPerGame,
            ExpectedPointsNext = epNext,
            DefensiveContributionPer90 = defensiveContributionPer90,
            Minutes = minutes,
            ExpectedGoalInvolvements = xgi,
            ExpectedGoalsConceded = xgc,
            PenaltiesOrder = penaltiesOrder,
            DirectFreekicksOrder = freekicksOrder,
            CornersAndIndirectFreekicksOrder = cornersOrder,
        };

    [Fact]
    public void EffectiveRate_FallsBackThroughForm_ThenEpNext_ThenPointsPerGame()
    {
        Assert.Equal(4.0, ExpectedPointsEngine.EffectiveRate(MakePlayer(form: "4.0", epNext: "9.0", pointsPerGame: "9.0"), null));
        Assert.Equal(3.0, ExpectedPointsEngine.EffectiveRate(MakePlayer(form: "0", epNext: "3.0", pointsPerGame: "9.0"), null));
        Assert.Equal(2.0, ExpectedPointsEngine.EffectiveRate(MakePlayer(form: "0", epNext: "0", pointsPerGame: "2.0"), null));
    }

    [Fact]
    public void EffectiveRate_IgnoresUnderlyingStats_BelowTwoMatchesOfMinutes()
    {
        // High xGI would pull the rate up if blended in, but 89 minutes isn't enough sample to trust.
        var player = MakePlayer(form: "2.0", minutes: 89, xgi: "5.0");

        Assert.Equal(2.0, ExpectedPointsEngine.EffectiveRate(player, null));
    }

    [Fact]
    public void EffectiveRate_BlendsInUnderlyingStats_OnceMinutesSampleIsLargeEnough()
    {
        var lowUnderlying = MakePlayer(form: "5.0", minutes: 900, xgi: "0.05"); // ~10 matches, weak xGI
        var highUnderlying = MakePlayer(form: "5.0", minutes: 900, xgi: "1.0"); // same form, much stronger xGI

        var lowRate = ExpectedPointsEngine.EffectiveRate(lowUnderlying, null);
        var highRate = ExpectedPointsEngine.EffectiveRate(highUnderlying, null);

        Assert.True(highRate > lowRate, "a player with much stronger underlying output should rate higher despite identical form");
    }

    [Fact]
    public void EffectiveRate_AppliesSetPieceBonus_ToDefendersAndMidfieldersOnDuty()
    {
        var withoutDuty = MakePlayer(elementType: 2, form: "3.0");
        var penaltyTaker = MakePlayer(elementType: 2, form: "3.0", penaltiesOrder: 1);
        var freekickTaker = MakePlayer(elementType: 3, form: "3.0", freekicksOrder: 1);
        var cornerTaker = MakePlayer(elementType: 3, form: "3.0", cornersOrder: 1);
        var backupTaker = MakePlayer(elementType: 2, form: "3.0", penaltiesOrder: 2); // 2nd in line, no bonus

        var baseline = ExpectedPointsEngine.EffectiveRate(withoutDuty, null);
        Assert.True(ExpectedPointsEngine.EffectiveRate(penaltyTaker, null) > baseline);
        Assert.True(ExpectedPointsEngine.EffectiveRate(freekickTaker, null) > baseline);
        Assert.True(ExpectedPointsEngine.EffectiveRate(cornerTaker, null) > baseline);
        Assert.Equal(baseline, ExpectedPointsEngine.EffectiveRate(backupTaker, null));
    }

    [Fact]
    public void EffectiveRate_DoesNotApplySetPieceBonus_ToGoalkeepersOrForwards()
    {
        var gk = MakePlayer(elementType: 1, form: "3.0", penaltiesOrder: 1);
        var fwd = MakePlayer(elementType: 4, form: "3.0", penaltiesOrder: 1);

        Assert.Equal(3.0, ExpectedPointsEngine.EffectiveRate(gk, null));
        Assert.Equal(3.0, ExpectedPointsEngine.EffectiveRate(fwd, null));
    }

    [Fact]
    public void EffectiveRate_RisesWithDefensiveContributionRate_ForDefendersAboveTheBlendThreshold()
    {
        var noDefensiveWork = MakePlayer(elementType: 2, form: "3.0", minutes: 900, defensiveContributionPer90: 0);
        var heavyDefensiveWork = MakePlayer(elementType: 2, form: "3.0", minutes: 900, defensiveContributionPer90: 10);

        Assert.True(
            ExpectedPointsEngine.EffectiveRate(heavyDefensiveWork, null) > ExpectedPointsEngine.EffectiveRate(noDefensiveWork, null),
            "a defender averaging the 10-action threshold every match should rate higher than one who never contributes defensively");
    }

    [Fact]
    public void EffectiveRate_UsesTheHigherAttackingThreshold_ForMidfieldersAndForwards()
    {
        // A rate of 10 clears the defender threshold comfortably but falls short of the higher
        // midfielder/forward one (12) — so a defender should get more credit for the same rate.
        var defenderAtTen = MakePlayer(elementType: 2, form: "3.0", minutes: 900, defensiveContributionPer90: 10);
        var midfielderAtTen = MakePlayer(elementType: 3, form: "3.0", minutes: 900, defensiveContributionPer90: 10);

        Assert.True(ExpectedPointsEngine.EffectiveRate(defenderAtTen, null) > ExpectedPointsEngine.EffectiveRate(midfielderAtTen, null));
    }

    [Fact]
    public void EffectiveRate_GivesGoalkeepersNoDefensiveContributionCredit()
    {
        var gk = MakePlayer(elementType: 1, form: "3.0", minutes: 900, defensiveContributionPer90: 20);
        var gkWithoutDefensiveWork = MakePlayer(elementType: 1, form: "3.0", minutes: 900, defensiveContributionPer90: 0);

        Assert.Equal(ExpectedPointsEngine.EffectiveRate(gkWithoutDefensiveWork, null), ExpectedPointsEngine.EffectiveRate(gk, null));
    }

    [Fact]
    public void EffectiveRate_ShrinksTowardTheReliabilityFloor_ForARarelyUsedPlayer()
    {
        // Minutes kept below the underlying-stats blend threshold (180) so only the minutes-
        // reliability shrinkage is being exercised here.
        var mostlyPlayedTeam = new Team { Id = 1, Played = 10 }; // 900 minutes available this season
        var barelyPlayed = MakePlayer(form: "6.0", minutes: 45); // one substitute cameo
        var mostlyPlayed = MakePlayer(form: "6.0", minutes: 170); // regular substitute appearances

        var lowReliabilityRate = ExpectedPointsEngine.EffectiveRate(barelyPlayed, mostlyPlayedTeam);
        var higherReliabilityRate = ExpectedPointsEngine.EffectiveRate(mostlyPlayed, mostlyPlayedTeam);

        Assert.True(lowReliabilityRate < higherReliabilityRate);
        Assert.True(lowReliabilityRate < 6.0 && higherReliabilityRate < 6.0);
        Assert.True(lowReliabilityRate >= 6.0 * 0.3, "reliability should never shrink below the floor multiplier");

        // A player who's played every available minute for a single-match-old team gets no shrinkage.
        var fullyReliableTeam = new Team { Id = 2, Played = 1 };
        var nailedOn = MakePlayer(form: "6.0", minutes: 90);
        Assert.Equal(6.0, ExpectedPointsEngine.EffectiveRate(nailedOn, fullyReliableTeam));
    }

    [Fact]
    public void EffectiveRate_DoesNotShrink_WhenTeamMatchesPlayedIsUnknown()
    {
        var player = MakePlayer(form: "6.0", minutes: 0);

        Assert.Equal(6.0, ExpectedPointsEngine.EffectiveRate(player, new Team { Id = 1, Played = 0 }));
    }

    [Fact]
    public void FixtureFactor_FallsBackToFdrOnly_WhenTeamStrengthIsUnavailable()
    {
        var factor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 2, playerTeam: null, opponentTeam: null, isHome: true);

        Assert.Equal((6.0 - 2) / 3.0, factor);
    }

    [Fact]
    public void FixtureFactor_GivesAStrongerTeamMoreCredit_ForTheSameFdrRatedFixture()
    {
        var strongTeam = new Team { Id = 1, StrengthOverallHome = 1400, StrengthOverallAway = 1400 };
        var weakTeam = new Team { Id = 2, StrengthOverallHome = 1000, StrengthOverallAway = 1000 };
        var opponent = new Team { Id = 3, StrengthOverallHome = 1200, StrengthOverallAway = 1200 };

        var strongTeamFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, strongTeam, opponent, isHome: true);
        var weakTeamFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, weakTeam, opponent, isHome: true);

        Assert.True(strongTeamFactor > weakTeamFactor, "a stronger team should get more credit for an identically FDR-rated fixture");
    }
}
