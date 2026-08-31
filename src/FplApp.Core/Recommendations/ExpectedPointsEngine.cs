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

    // Extra goal threat for a nailed-on penalty/free-kick/corner taker that isn't fully reflected in
    // their season-average involvement rate yet (e.g. they only inherited the duty recently).
    private const double SetPieceBonus = 0.3;

    // A player who's featured for only a fraction of their team's available minutes this season is
    // less certain to start future matches than their per-90 output alone implies, even if currently
    // marked available — this is the floor that rate gets shrunk toward as minutes share drops to 0.
    private const double MinReliabilityMultiplier = 0.3;

    private const int GoalkeeperType = 1;
    private const int DefenderType = 2;
    private const int MidfielderType = 3;

    /// <summary>Expected points for one specific fixture — the rate signal scaled by fixture difficulty.</summary>
    public static double EstimatePoints(Player player, Team? playerTeam, int fplDifficulty, Team? opponentTeam, bool isHome)
        => EffectiveRate(player, playerTeam) * FixtureFactor(fplDifficulty, playerTeam, opponentTeam, isHome);

    /// <summary>
    /// The blended per-fixture point rate: recent form (or FPL's own next-gameweek prediction, or
    /// season points-per-game, whichever is the best signal available), blended with an
    /// underlying-stats estimate once there's enough of a minutes sample to trust it, boosted for
    /// primary set-piece takers, and shrunk for players who haven't nailed down regular minutes.
    /// </summary>
    public static double EffectiveRate(Player player, Team? playerTeam)
    {
        var actualRate = ActualRate(player);
        var blended = BlendWithUnderlyingStats(player, actualRate);
        var withSetPieces = blended + PlayerSetPieceBonus(player);
        return withSetPieces * MinutesReliability(player, playerTeam);
    }

    /// <summary>
    /// Blends FPL's own 1-5 fixture difficulty rating with a continuous factor derived from each
    /// team's actual current-season overall strength — FPL's FDR only reflects the opponent, so it
    /// rates the same opponent identically for a title-chasing team and a relegation-battler even
    /// though the strong team should get more credit for an "easy" fixture. Falls back to the plain
    /// FDR-only factor when strength data isn't available (e.g. in tests, or teams before the ratings
    /// have settled).
    /// </summary>
    public static double FixtureFactor(int fplDifficulty, Team? playerTeam, Team? opponentTeam, bool isHome)
    {
        var fdrFactor = (6.0 - fplDifficulty) / 3.0;
        if (playerTeam is null || opponentTeam is null)
        {
            return fdrFactor;
        }

        var ownStrength = isHome ? playerTeam.StrengthOverallHome : playerTeam.StrengthOverallAway;
        var oppStrength = isHome ? opponentTeam.StrengthOverallAway : opponentTeam.StrengthOverallHome;
        if (ownStrength <= 0 || oppStrength <= 0)
        {
            return fdrFactor;
        }

        var strengthFactor = Math.Clamp((double)ownStrength / oppStrength, 0.5, 2.0);
        return (fdrFactor + strengthFactor) / 2.0;
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
    private static double BlendWithUnderlyingStats(Player player, double actualRate)
    {
        if (player.Minutes < MinMinutesForUnderlyingBlend)
        {
            return actualRate;
        }

        var underlyingRate = UnderlyingRate(player);
        return underlyingRate <= 0 ? actualRate : (actualRate * (1 - UnderlyingStatsWeight)) + (underlyingRate * UnderlyingStatsWeight);
    }

    private static double UnderlyingRate(Player player)
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
        var attackingPoints = xgiPerMatch * pointsPerGoalInvolvement;

        var defensivePoints = 0.0;
        if (player.ElementType is GoalkeeperType or DefenderType)
        {
            var xgcPerMatch = ParseDecimal(player.ExpectedGoalsConceded) / matchesPlayed;
            // Rough logistic-style approximation: expected goals conceded of 0 implies a clean sheet
            // is close to certain, 1.5+ implies it's close to never happening.
            var cleanSheetProbability = Math.Clamp(1.0 - (xgcPerMatch / 1.5), 0, 1);
            defensivePoints = cleanSheetProbability * 4.0;
        }

        const double appearancePoints = 2.0; // guaranteed for any player who starts (60+ minutes)
        return attackingPoints + defensivePoints + appearancePoints;
    }

    private static double PlayerSetPieceBonus(Player player)
    {
        if (player.ElementType is not (DefenderType or MidfielderType))
        {
            return 0; // forwards are already expected to be a goal threat; GKs don't take set pieces
        }

        var isPrimaryTaker = player.PenaltiesOrder == 1 || player.DirectFreekicksOrder == 1 || player.CornersAndIndirectFreekicksOrder == 1;
        return isPrimaryTaker ? SetPieceBonus : 0;
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

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
