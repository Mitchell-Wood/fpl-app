using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// The single source of truth for "how many points is this player worth in one fixture" — used by
/// every recommendation surface (captaincy, lineup optimizer, transfer suggestions, squad builder)
/// so a tuning change only has to be made once and applies everywhere consistently.
/// </summary>
public static class ExpectedPointsEngine
{
    // How much the blended rate leans on underlying stats (xGI, expected goals conceded) once a
    // player has a large enough sample — actual returns are noisy (bonus points, deflections,
    // finishing variance) and regress toward the underlying rate over time, so blending in xG-based
    // output smooths that noise rather than chasing whatever a player's recent bounces happened to be.
    private const double UnderlyingStatsWeight = 0.35;

    // Below two full matches of minutes, per-90 underlying stats are too small a sample to trust —
    // fall back to the plain form/ep_next/points-per-game rate instead of blending them in.
    private const double MinMinutesForUnderlyingBlend = 180;

    // Extra points for a nailed-on set-piece duty that isn't fully reflected in a player's
    // season-average involvement rate yet (e.g. they only inherited the duty recently). The three
    // duties aren't worth the same and are kept as separate constants (added together for a player who
    // holds more than one) rather than one flat bonus:
    //  - Penalties convert at a high rate (~75-80%) and are effectively a free, high-probability shot
    //    roughly every 5-7 matches per team, making the primary taker's duty the most valuable by far.
    //  - Direct free kicks are a much rarer scoring opportunity — most direct free kicks in a match
    //    aren't even in a shooting position — so a nailed-on taker earns a modest bump.
    //  - Corners/indirect free kicks essentially never produce a goal for the taker themselves, but a
    //    consistent delivery threat meaningfully raises their assist chances over a season.
    // These are rough season-average estimates rather than derived from per-team penalty frequency or
    // conversion-rate data, which isn't available here.
    private const double PenaltyTakerBonus = 0.45;
    private const double FreekickTakerBonus = 0.15;
    private const double CornerTakerBonus = 0.1;

    // A player who's featured for only a fraction of their team's available minutes this season is
    // less certain to start future matches than their per-90 output alone implies, even if currently
    // marked available — this is the floor that rate gets shrunk toward as minutes share drops to 0.
    private const double MinReliabilityMultiplier = 0.3;

    // FPL awards 2pts for hitting a minimum number of defensive actions (CBIT) in a single match:
    // 10 for defenders, 12 for midfielders/forwards.
    private const int DefenderDefensiveContributionThreshold = 10;
    private const int AttackingDefensiveContributionThreshold = 12;
    private const double DefensiveContributionPoints = 2.0;

    private const int GoalkeeperType = 1;
    private const int DefenderType = 2;
    private const int MidfielderType = 3;
    private const int ForwardType = 4;

    /// <summary>Expected points for one specific fixture — the rate signal scaled by fixture difficulty.</summary>
    /// <param name="weeksAhead">
    /// How many gameweeks out this fixture is from the next one (0 = the next gameweek). Only affects
    /// how much a live fitness doubt discounts the estimate — see <see cref="PlayingChanceReliability"/>.
    /// </param>
    public static double EstimatePoints(Player player, Team? playerTeam, int fplDifficulty, Team? opponentTeam, bool isHome, int weeksAhead = 0)
        => EffectiveRate(player, playerTeam, weeksAhead, MatchGoalEnvironmentFactor(fplDifficulty, playerTeam, opponentTeam, isHome))
            * FixtureFactor(fplDifficulty, playerTeam, opponentTeam, isHome, player.ElementType);

    /// <summary>
    /// The blended per-fixture point rate: recent form (or FPL's own next-gameweek prediction, or
    /// season points-per-game, whichever is the best signal available), blended with an
    /// underlying-stats estimate once there's enough of a minutes sample to trust it, boosted for
    /// primary set-piece takers, and shrunk both for players who haven't nailed down regular minutes
    /// and for a live fitness doubt on the next fixture.
    /// </summary>
    /// <param name="bonusMatchIntensity">
    /// See <see cref="MatchGoalEnvironmentFactor"/> — scales only the bonus-points component, not the
    /// whole rate, so it stacks with (rather than duplicates) the broader fixture-difficulty scaling
    /// applied separately in <see cref="EstimatePoints"/>. Defaults to neutral for callers that score a
    /// player outside the context of one specific fixture.
    /// </param>
    public static double EffectiveRate(Player player, Team? playerTeam, int weeksAhead = 0, double bonusMatchIntensity = 1.0)
    {
        var actualRate = ActualRate(player);
        var blended = BlendWithUnderlyingStats(player, actualRate, bonusMatchIntensity);
        var withSetPieces = blended + PlayerSetPieceBonus(player);
        return withSetPieces * MinutesReliability(player, playerTeam) * PlayingChanceReliability(player, weeksAhead);
    }

    /// <summary>
    /// Blends FPL's own 1-5 fixture difficulty rating with a continuous factor derived from each
    /// team's actual current-season strength — FPL's FDR only reflects the opponent, so it rates the
    /// same opponent identically for a title-chasing team and a relegation-battler even though the
    /// strong team should get more credit for an "easy" fixture. Uses position-specific strength (a
    /// defensive player's clean-sheet chance depends on their team's defence vs the opponent's
    /// attack; an attacking player's scoring chance depends on the reverse) rather than each team's
    /// overall rating. Falls back to the plain FDR-only factor when strength data isn't available
    /// (e.g. in tests, or before a season's ratings have settled).
    /// </summary>
    public static double FixtureFactor(int fplDifficulty, Team? playerTeam, Team? opponentTeam, bool isHome, int elementType)
    {
        var fdrFactor = (6.0 - fplDifficulty) / 3.0;
        if (playerTeam is null || opponentTeam is null)
        {
            return fdrFactor;
        }

        var isDefensivePosition = elementType is GoalkeeperType or DefenderType;
        var ownStrength = isDefensivePosition
            ? (isHome ? playerTeam.StrengthDefenceHome : playerTeam.StrengthDefenceAway)
            : (isHome ? playerTeam.StrengthAttackHome : playerTeam.StrengthAttackAway);
        var oppStrength = isDefensivePosition
            ? (isHome ? opponentTeam.StrengthAttackAway : opponentTeam.StrengthAttackHome)
            : (isHome ? opponentTeam.StrengthDefenceAway : opponentTeam.StrengthDefenceHome);

        if (ownStrength <= 0 || oppStrength <= 0)
        {
            // FPL doesn't always populate the position-specific attack/defence split (e.g. it's all
            // zeroes early in a season) — fall back to each team's overall strength rather than
            // giving up the team-strength signal entirely.
            ownStrength = isHome ? playerTeam.StrengthOverallHome : playerTeam.StrengthOverallAway;
            oppStrength = isHome ? opponentTeam.StrengthOverallAway : opponentTeam.StrengthOverallHome;
        }
        if (ownStrength <= 0 || oppStrength <= 0)
        {
            return fdrFactor;
        }

        var strengthFactor = Math.Clamp((double)ownStrength / oppStrength, 0.5, 2.0);
        return (fdrFactor + strengthFactor) / 2.0;
    }

    /// <summary>
    /// How goal-heavy a fixture is likely to be for either side — used to scale expected bonus points,
    /// since BPS-worthy moments (goals, assists, big defensive actions) cluster in open, high-scoring
    /// matches regardless of which team a player is on. Averages both teams' attacking prospects
    /// (each side's attack vs the other's defence, via <see cref="FixtureFactor"/> as an attacker would
    /// see it) rather than just the player's own side, since bonus points aren't limited to whichever
    /// team is "in form" going in — a relegation-battler grinding out a 0-0 suppresses bonus for
    /// everyone on the pitch just as much as it does clean-sheet points for the other side.
    /// </summary>
    private static double MatchGoalEnvironmentFactor(int fplDifficulty, Team? playerTeam, Team? opponentTeam, bool isHome)
    {
        // Reuses the player's own-side FDR for the reversed (opponent's-attack) calculation too, since
        // the opponent's own FDR rating for this fixture isn't available here — an approximation, but
        // FDR is only half of what FixtureFactor blends in, with the correctly-swapped team-strength
        // ratio carrying the rest.
        var ownAttack = FixtureFactor(fplDifficulty, playerTeam, opponentTeam, isHome, MidfielderType);
        var oppAttack = FixtureFactor(fplDifficulty, opponentTeam, playerTeam, !isHome, MidfielderType);
        return (ownAttack + oppAttack) / 2.0;
    }

    private static double ActualRate(Player player)
    {
        var form = ParseDecimal(player.Form);
        if (form > 0)
        {
            return form;
        }

        var expectedPointsNext = ParseDecimal(player.ExpectedPointsNext);
        return expectedPointsNext > 0 ? expectedPointsNext : ParseDecimal(player.PointsPerGame);
    }

    /// <summary>
    /// Converts season-cumulative expected-goal-involvement and expected-goals-conceded into an
    /// approximate points-per-match figure, then blends it with the actual-points-based rate — the
    /// standard "xG regression" idea that underlying chance quality is a more stable predictor of
    /// future output than a small sample of actual (lucky-or-unlucky) results.
    /// </summary>
    private static double BlendWithUnderlyingStats(Player player, double actualRate, double bonusMatchIntensity)
    {
        if (player.Minutes < MinMinutesForUnderlyingBlend)
        {
            return actualRate;
        }

        var underlyingRate = UnderlyingRate(player, bonusMatchIntensity);
        return underlyingRate <= 0 ? actualRate : (actualRate * (1 - UnderlyingStatsWeight)) + (underlyingRate * UnderlyingStatsWeight);
    }

    private static double UnderlyingRate(Player player, double bonusMatchIntensity)
    {
        var matchesPlayed = player.Minutes / 90.0;
        if (matchesPlayed <= 0)
        {
            return 0;
        }

        var xgiPerMatch = ParseDecimal(player.ExpectedGoalInvolvements) / matchesPlayed;

        // FPL scores goals higher for defensive positions (6pts GK/DEF, 5pts MID, 4pts FWD) and
        // assists at 3pts regardless of position — approximated here with a typical 2:1 goal:assist
        // split among a player's goal involvements.
        var pointsPerGoalInvolvement = player.ElementType switch
        {
            GoalkeeperType or DefenderType => ((6.0 * 2) + 3.0) / 3.0,
            MidfielderType => ((5.0 * 2) + 3.0) / 3.0,
            _ => ((4.0 * 2) + 3.0) / 3.0,
        };

        // FPL's Threat sub-index (built from shots, shot location, and box-entry frequency) is a
        // second, independently-modeled signal of a player's underlying goal threat — distinct from
        // xG's shot-by-shot model — so blending a modest amount of it in helps catch attacking
        // involvement that isn't yet reflected in a still-small xG sample. Threat has no natural
        // points scale of its own, so this uses a conservative, hand-picked conversion rather than one
        // derived from real calibration data (which isn't available here) — small enough to nudge,
        // not dominate, the xG-based estimate.
        var threatPerMatch = ParseDecimal(player.Threat) / matchesPlayed;
        const double threatToPointsScale = 0.008;
        var threatPoints = threatPerMatch * threatToPointsScale;

        var attackingPoints = (xgiPerMatch * pointsPerGoalInvolvement) + threatPoints;

        var xgcPerMatch = ParseDecimal(player.ExpectedGoalsConceded) / matchesPlayed;
        // Goals conceded in a match are well-approximated as Poisson-distributed around the team's
        // expected-goals-conceded rate, so P(clean sheet) = P(0 goals) = e^-xgc — the same Poisson
        // reasoning already used for defensive-contribution points, rather than an ad-hoc linear taper.
        var cleanSheetProbability = Math.Exp(-xgcPerMatch);

        var defensivePoints = player.ElementType switch
        {
            // GK/DEF: 4pts for a clean sheet, minus ~1pt per 2 goals conceded (FPL's actual rule).
            GoalkeeperType or DefenderType => (cleanSheetProbability * 4.0) - (xgcPerMatch * 0.5),
            // MID also earns 1pt for a clean sheet (but has no goals-conceded penalty).
            MidfielderType => cleanSheetProbability * 1.0,
            _ => 0.0,
        };

        // 1pt per 3 saves — goalkeeper-only, and already a per-match rate so no matches-played
        // division needed.
        var savesPoints = player.ElementType == GoalkeeperType ? player.SavesPer90 / 3.0 : 0.0;

        // The season's own bonus-points rate is used as a proxy for future bonus (BPS rewards goals,
        // assists, clean sheets, and defensive actions — the same inputs already driving the rest of
        // this estimate — so a player's historical bonus rate is a reasonable stand-in for a bonus
        // system that isn't otherwise modeled here), scaled by how goal-heavy this specific fixture is
        // expected to be (see MatchGoalEnvironmentFactor) rather than treated as a flat season average
        // regardless of opponent.
        var bonusPoints = (player.Bonus / matchesPlayed) * bonusMatchIntensity;

        // Card-prone players lose a small amount of expected value every match: -1pt per yellow,
        // -3pts per red, at their season rate.
        var cardPenalty = -((player.YellowCards / matchesPlayed) + (3.0 * player.RedCards / matchesPlayed));

        const double appearancePoints = 2.0; // guaranteed for any player who starts (60+ minutes)
        return attackingPoints + defensivePoints + savesPoints + bonusPoints + cardPenalty + appearancePoints + DefensiveContributionPointsFor(player);
    }

    /// <summary>
    /// Expected value of the defensive-contribution bonus point, treating a player's per-match count
    /// of defensive actions as Poisson-distributed around their season per-90 rate — a standard way
    /// to turn a per-90 average into "probability of clearing a fixed threshold in one match" for a
    /// count-based stat like this.
    /// </summary>
    private static double DefensiveContributionPointsFor(Player player)
    {
        var threshold = player.ElementType switch
        {
            DefenderType => DefenderDefensiveContributionThreshold,
            MidfielderType or ForwardType => AttackingDefensiveContributionThreshold,
            _ => (int?)null, // goalkeepers aren't eligible for the defensive-contribution point
        };

        return threshold is null ? 0 : DefensiveContributionPoints * PoissonProbabilityAtLeast(threshold.Value, player.DefensiveContributionPer90);
    }

    private static double PoissonProbabilityAtLeast(int threshold, double meanPerMatch)
    {
        if (meanPerMatch <= 0)
        {
            return 0;
        }

        // P(X >= threshold) = 1 - P(X <= threshold-1), built up term-by-term from the Poisson pmf
        // (pmf(n) = pmf(n-1) * lambda / n) rather than computing factorials directly.
        var pmf = Math.Exp(-meanPerMatch);
        var cdf = pmf;
        for (var n = 1; n < threshold; n++)
        {
            pmf *= meanPerMatch / n;
            cdf += pmf;
        }

        return Math.Clamp(1 - cdf, 0, 1);
    }

    private static double PlayerSetPieceBonus(Player player)
    {
        if (player.ElementType is not (DefenderType or MidfielderType))
        {
            return 0; // forwards are already expected to be a goal threat; GKs don't take set pieces
        }

        double bonus = 0;
        if (player.PenaltiesOrder == 1)
        {
            bonus += PenaltyTakerBonus;
        }
        if (player.DirectFreekicksOrder == 1)
        {
            bonus += FreekickTakerBonus;
        }
        if (player.CornersAndIndirectFreekicksOrder == 1)
        {
            bonus += CornerTakerBonus;
        }
        return bonus;
    }

    private static double MinutesReliability(Player player, Team? playerTeam)
    {
        if (playerTeam is null || playerTeam.Played <= 0)
        {
            return 1.0;
        }

        var minutesShare = Math.Clamp(player.Minutes / (playerTeam.Played * 90.0), 0, 1);
        return MinReliabilityMultiplier + ((1 - MinReliabilityMultiplier) * minutesShare);
    }

    // FPL's "chance of playing next round" is a live, short-term fitness call (a knock, a late test)
    // that says nothing about a player's availability two or three weeks out — by then they've either
    // recovered or a longer-term injury has replaced it with fresh news. Fading the discount to zero
    // over this many gameweeks avoids carrying "75% for this week" as a permanent penalty into a
    // lookahead window where it no longer applies.
    private const double PlayingChanceDecayWeeks = 2.0;

    /// <summary>
    /// FPL's own "chance of playing next round" percentage, applied as a straight probability discount
    /// to the next fixture and faded out over <see cref="PlayingChanceDecayWeeks"/> gameweeks for
    /// fixtures further out in the lookahead window, since the percentage only ever describes the
    /// immediate next round.
    /// </summary>
    private static double PlayingChanceReliability(Player player, int weeksAhead)
    {
        if (player.ChanceOfPlayingNextRound is not { } chance)
        {
            return 1.0;
        }

        var discount = 1.0 - Math.Clamp(chance / 100.0, 0, 1);
        var decayWeight = Math.Clamp(1.0 - (weeksAhead / PlayingChanceDecayWeeks), 0, 1);
        return 1.0 - (discount * decayWeight);
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
