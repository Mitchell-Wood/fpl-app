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
        string threat = "0",
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
            Threat = threat,
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
    public void EffectiveRate_CreditsHigherThreat_AtEqualXgi()
    {
        var lowThreat = MakePlayer(form: "5.0", minutes: 900, xgi: "5.0", threat: "10.0");
        var highThreat = MakePlayer(form: "5.0", minutes: 900, xgi: "5.0", threat: "200.0");

        var lowRate = ExpectedPointsEngine.EffectiveRate(lowThreat, null);
        var highRate = ExpectedPointsEngine.EffectiveRate(highThreat, null);

        Assert.True(highRate > lowRate, "higher underlying threat should nudge the rate up even at identical xGI");
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
    public void EffectiveRate_ValuesAPenaltyTaker_AboveAFreekickTaker_AboveACornerTaker()
    {
        var penaltyTaker = MakePlayer(elementType: 2, form: "3.0", penaltiesOrder: 1);
        var freekickTaker = MakePlayer(elementType: 2, form: "3.0", freekicksOrder: 1);
        var cornerTaker = MakePlayer(elementType: 2, form: "3.0", cornersOrder: 1);

        var penaltyRate = ExpectedPointsEngine.EffectiveRate(penaltyTaker, null);
        var freekickRate = ExpectedPointsEngine.EffectiveRate(freekickTaker, null);
        var cornerRate = ExpectedPointsEngine.EffectiveRate(cornerTaker, null);

        Assert.True(penaltyRate > freekickRate, "a nailed-on penalty taker should be worth more than a free-kick-only taker");
        Assert.True(freekickRate > cornerRate, "a free-kick taker should be worth more than a corner-only taker");
    }

    [Fact]
    public void EffectiveRate_StacksSetPieceDuties_ForAPlayerWhoHoldsMoreThanOne()
    {
        var penaltyOnly = MakePlayer(elementType: 2, form: "3.0", penaltiesOrder: 1);
        var penaltyAndFreekicks = MakePlayer(elementType: 2, form: "3.0", penaltiesOrder: 1, freekicksOrder: 1);

        Assert.True(
            ExpectedPointsEngine.EffectiveRate(penaltyAndFreekicks, null) > ExpectedPointsEngine.EffectiveRate(penaltyOnly, null),
            "holding an additional set-piece duty should add further value, not be capped at one flat bonus");
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
        var factor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 2, playerTeam: null, opponentTeam: null, isHome: true, elementType: 3);

        Assert.Equal((6.0 - 2) / 3.0, factor);
    }

    [Fact]
    public void FixtureFactor_GivesAStrongerAttackMoreCredit_ForAMidfielder_OnTheSameFdrRatedFixture()
    {
        var strongAttackTeam = new Team { Id = 1, StrengthAttackHome = 1400, StrengthDefenceHome = 1200 };
        var weakAttackTeam = new Team { Id = 2, StrengthAttackHome = 1000, StrengthDefenceHome = 1200 };
        var opponent = new Team { Id = 3, StrengthAttackAway = 1200, StrengthDefenceAway = 1200 };

        var strongAttackFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, strongAttackTeam, opponent, isHome: true, elementType: 3);
        var weakAttackFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, weakAttackTeam, opponent, isHome: true, elementType: 3);

        Assert.True(strongAttackFactor > weakAttackFactor, "a stronger attack should get more credit for an identically FDR-rated fixture");
    }

    [Fact]
    public void FixtureFactor_GivesAStrongerDefenceMoreCredit_ForADefender_OnTheSameFdrRatedFixture()
    {
        var strongDefenceTeam = new Team { Id = 1, StrengthDefenceHome = 1400, StrengthAttackHome = 1200 };
        var weakDefenceTeam = new Team { Id = 2, StrengthDefenceHome = 1000, StrengthAttackHome = 1200 };
        var opponent = new Team { Id = 3, StrengthAttackAway = 1200, StrengthDefenceAway = 1200 };

        var strongDefenceFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, strongDefenceTeam, opponent, isHome: true, elementType: 2);
        var weakDefenceFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, weakDefenceTeam, opponent, isHome: true, elementType: 2);

        Assert.True(strongDefenceFactor > weakDefenceFactor, "a stronger defence should get more credit for an identically FDR-rated fixture");
    }

    [Fact]
    public void FixtureFactor_FallsBackToOverallStrength_WhenPositionSpecificStrengthIsAllZero()
    {
        // Reflects real FPL data: strength_attack_*/strength_defence_* are all zero at points in a
        // season, even though strength_overall_* is populated — the signal shouldn't be lost.
        var strongTeam = new Team { Id = 1, StrengthOverallHome = 5, StrengthOverallAway = 5 };
        var weakTeam = new Team { Id = 2, StrengthOverallHome = 2, StrengthOverallAway = 2 };
        var opponent = new Team { Id = 3, StrengthOverallHome = 3, StrengthOverallAway = 3 };

        var strongTeamFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, strongTeam, opponent, isHome: true, elementType: 2);
        var weakTeamFactor = ExpectedPointsEngine.FixtureFactor(fplDifficulty: 3, weakTeam, opponent, isHome: true, elementType: 2);

        Assert.True(strongTeamFactor > weakTeamFactor);
    }

    [Fact]
    public void EffectiveRate_DiscountsForALiveFitnessDoubt()
    {
        var fit = MakePlayer(form: "6.0");
        fit.ChanceOfPlayingNextRound = 100;
        var doubtful = MakePlayer(form: "6.0");
        doubtful.ChanceOfPlayingNextRound = 50;

        Assert.Equal(6.0, ExpectedPointsEngine.EffectiveRate(fit, null));
        Assert.Equal(3.0, ExpectedPointsEngine.EffectiveRate(doubtful, null));
    }

    [Fact]
    public void EffectiveRate_AppliesNoFitnessDiscount_WhenChanceOfPlayingIsUnset()
    {
        var player = MakePlayer(form: "6.0");
        Assert.Null(player.ChanceOfPlayingNextRound);

        Assert.Equal(6.0, ExpectedPointsEngine.EffectiveRate(player, null));
    }

    [Fact]
    public void EffectiveRate_CreditsMidfieldersForCleanSheets_ButNotForwards()
    {
        var midWithCleanSheetChance = MakePlayer(elementType: 3, form: "5.0", minutes: 900, xgc: "0"); // xGC 0 -> ~certain clean sheet
        var midWithNoCleanSheetChance = MakePlayer(elementType: 3, form: "5.0", minutes: 900, xgc: "3.0"); // high xGC -> ~no clean sheet
        Assert.True(
            ExpectedPointsEngine.EffectiveRate(midWithCleanSheetChance, null) > ExpectedPointsEngine.EffectiveRate(midWithNoCleanSheetChance, null),
            "a midfielder likely to keep a clean sheet should rate higher than one who isn't");

        var fwdWithCleanSheetChance = MakePlayer(elementType: 4, form: "5.0", minutes: 900, xgc: "0");
        var fwdWithNoCleanSheetChance = MakePlayer(elementType: 4, form: "5.0", minutes: 900, xgc: "3.0");
        Assert.Equal(
            ExpectedPointsEngine.EffectiveRate(fwdWithCleanSheetChance, null),
            ExpectedPointsEngine.EffectiveRate(fwdWithNoCleanSheetChance, null));
    }

    [Fact]
    public void EffectiveRate_CreditsGoalkeepersForSaves()
    {
        var busyKeeper = MakePlayer(elementType: 1, form: "2.0", minutes: 900);
        busyKeeper.SavesPer90 = 6.0;
        var quietKeeper = MakePlayer(elementType: 1, form: "2.0", minutes: 900);
        quietKeeper.SavesPer90 = 0.0;

        Assert.True(ExpectedPointsEngine.EffectiveRate(busyKeeper, null) > ExpectedPointsEngine.EffectiveRate(quietKeeper, null));
    }

    [Fact]
    public void EffectiveRate_PenalizesACardProneHistory()
    {
        var cardProne = MakePlayer(elementType: 2, form: "5.0", minutes: 900);
        cardProne.YellowCards = 8; // roughly 0.8/match over 10 matches

        var cardFree = MakePlayer(elementType: 2, form: "5.0", minutes: 900);

        Assert.True(ExpectedPointsEngine.EffectiveRate(cardProne, null) < ExpectedPointsEngine.EffectiveRate(cardFree, null));
    }

    [Fact]
    public void EffectiveRate_CreditsAHistoricalBonusRate()
    {
        var bonusMagnet = MakePlayer(elementType: 3, form: "5.0", minutes: 900);
        bonusMagnet.Bonus = 20; // 2/match over 10 matches

        var noBonusHistory = MakePlayer(elementType: 3, form: "5.0", minutes: 900);

        Assert.True(ExpectedPointsEngine.EffectiveRate(bonusMagnet, null) > ExpectedPointsEngine.EffectiveRate(noBonusHistory, null));
    }

    [Fact]
    public void EffectiveRate_DerivesCleanSheetProbabilityFromPoisson()
    {
        // xGC of 0.5/match -> P(0 goals conceded) = e^-0.5, worth 1pt to a clean-sheet MID (via the
        // 35%-weighted underlying-stats blend, since form/points-per-game are both 0 here).
        var mid = MakePlayer(elementType: 3, form: "0", minutes: 900, xgc: "5.0"); // 5.0 over 10 matches = 0.5/match
        const double appearancePoints = 2.0;
        const double underlyingStatsWeight = 0.35;
        var expected = underlyingStatsWeight * (appearancePoints + (Math.Exp(-0.5) * 1.0));

        Assert.Equal(expected, ExpectedPointsEngine.EffectiveRate(mid, null), precision: 6);
    }

    [Fact]
    public void EstimatePoints_GivesAHigherEstimate_ForABonusMagnetInAnOpenHighScoringFixture()
    {
        var bonusMagnet = MakePlayer(elementType: 3, form: "0", minutes: 900);
        bonusMagnet.Bonus = 20;

        // Two attack-heavy, defence-light teams facing off (an open, likely high-scoring match) vs.
        // two defence-heavy, attack-light teams (a likely stalemate) — same FDR either way.
        var openPlayerTeam = new Team { Id = 1, Played = 10, StrengthAttackHome = 1400, StrengthDefenceHome = 1000 };
        var openOpponentTeam = new Team { Id = 2, StrengthAttackAway = 1400, StrengthDefenceAway = 1000 };
        var open = ExpectedPointsEngine.EstimatePoints(bonusMagnet, openPlayerTeam, fplDifficulty: 3, openOpponentTeam, isHome: true);

        var closedPlayerTeam = new Team { Id = 1, Played = 10, StrengthAttackHome = 1000, StrengthDefenceHome = 1400 };
        var closedOpponentTeam = new Team { Id = 2, StrengthAttackAway = 1000, StrengthDefenceAway = 1400 };
        var closed = ExpectedPointsEngine.EstimatePoints(bonusMagnet, closedPlayerTeam, fplDifficulty: 3, closedOpponentTeam, isHome: true);

        Assert.True(open > closed, "a bonus-reliant player should be rated higher in a fixture both teams are expected to attack in");
    }

    [Fact]
    public void EffectiveRate_ScalesBonusPointsByMatchIntensity()
    {
        var bonusMagnet = MakePlayer(elementType: 3, form: "0", minutes: 900);
        bonusMagnet.Bonus = 20; // 2.0/match over 10 matches

        var lowIntensity = ExpectedPointsEngine.EffectiveRate(bonusMagnet, null, weeksAhead: 0, bonusMatchIntensity: 0.5);
        var highIntensity = ExpectedPointsEngine.EffectiveRate(bonusMagnet, null, weeksAhead: 0, bonusMatchIntensity: 1.5);

        // Only the bonus term (2.0pts/match) is scaled, at the 35% underlying-stats blend weight:
        // (1.5 - 0.5) * 2.0 * 0.35 = 0.7.
        Assert.Equal(0.7, highIntensity - lowIntensity, precision: 6);
    }

    [Fact]
    public void EffectiveRate_MatchIntensityHasNoEffect_ForAPlayerWithNoBonusHistory()
    {
        var noBonusHistory = MakePlayer(elementType: 3, form: "0", minutes: 900);

        var lowIntensity = ExpectedPointsEngine.EffectiveRate(noBonusHistory, null, weeksAhead: 0, bonusMatchIntensity: 0.5);
        var highIntensity = ExpectedPointsEngine.EffectiveRate(noBonusHistory, null, weeksAhead: 0, bonusMatchIntensity: 1.5);

        Assert.Equal(lowIntensity, highIntensity, precision: 6);
    }

    [Fact]
    public void EffectiveRate_FadesTheFitnessDiscountAsWeeksAheadIncreases()
    {
        var doubtful = MakePlayer(form: "6.0");
        doubtful.ChanceOfPlayingNextRound = 50;

        Assert.Equal(3.0, ExpectedPointsEngine.EffectiveRate(doubtful, null, weeksAhead: 0));
        Assert.Equal(4.5, ExpectedPointsEngine.EffectiveRate(doubtful, null, weeksAhead: 1));
        Assert.Equal(6.0, ExpectedPointsEngine.EffectiveRate(doubtful, null, weeksAhead: 2));
        Assert.Equal(6.0, ExpectedPointsEngine.EffectiveRate(doubtful, null, weeksAhead: 5));
    }
}
