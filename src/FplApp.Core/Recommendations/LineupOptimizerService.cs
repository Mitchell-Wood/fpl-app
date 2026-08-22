using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Picks the best legal starting XI (plus captain/vice-captain) from an existing 15-man squad for
/// one specific gameweek — no transfers involved, just deciding who starts, who's benched, and who
/// wears the armband. Mirrors the projection formula used elsewhere (recent form, or FPL's own
/// points-per-game as a fallback, scaled by fixture difficulty), maximized subject to a legal
/// formation (1 GK, 3-5 DEF, 2-5 MID, 1-3 FWD).
/// </summary>
public class LineupOptimizerService
{
    private const int GoalkeeperType = 1;
    private const int DefenderType = 2;
    private const int MidfielderType = 3;
    private const int ForwardType = 4;

    private static readonly int[] DefenderCounts = [3, 4, 5];
    private static readonly int[] MidfielderCounts = [2, 3, 4, 5];

    // Unavailable players are only ever picked if there's no legal alternative to fill a slot.
    private const double UnavailablePenalty = -1000;

    public RecommendedLineup OptimizeLineup(BootstrapStatic bootstrap, IReadOnlyList<Fixture> fixtures, TeamPicks picks, int eventId)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(picks);

        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var eventFixtures = fixtures.Where(f => f.Event == eventId).ToList();

        var scored = new List<(int PlayerId, int ElementType, double ExpectedPoints)>();
        foreach (var pick in picks.Picks)
        {
            if (!playersById.TryGetValue(pick.Element, out var player))
            {
                continue;
            }

            scored.Add((player.Id, player.ElementType, ExpectedPointsFor(player, eventFixtures)));
        }

        var byType = new Dictionary<int, List<(int PlayerId, double ExpectedPoints)>>
        {
            [GoalkeeperType] = RankedByType(scored, GoalkeeperType),
            [DefenderType] = RankedByType(scored, DefenderType),
            [MidfielderType] = RankedByType(scored, MidfielderType),
            [ForwardType] = RankedByType(scored, ForwardType),
        };

        (int Def, int Mid, int Fwd, double Total)? best = null;
        foreach (var defCount in DefenderCounts)
        {
            foreach (var midCount in MidfielderCounts)
            {
                var fwdCount = 10 - defCount - midCount;
                if (fwdCount is < 1 or > 3)
                {
                    continue;
                }
                if (byType[GoalkeeperType].Count < 1 || byType[DefenderType].Count < defCount
                    || byType[MidfielderType].Count < midCount || byType[ForwardType].Count < fwdCount)
                {
                    continue;
                }

                var total = byType[GoalkeeperType][0].ExpectedPoints
                    + byType[DefenderType].Take(defCount).Sum(p => p.ExpectedPoints)
                    + byType[MidfielderType].Take(midCount).Sum(p => p.ExpectedPoints)
                    + byType[ForwardType].Take(fwdCount).Sum(p => p.ExpectedPoints);

                if (best is null || total > best.Value.Total)
                {
                    best = (defCount, midCount, fwdCount, total);
                }
            }
        }

        var result = new RecommendedLineup();
        if (best is null)
        {
            // Squad shape doesn't allow forming a legal XI (shouldn't happen for a real FPL squad).
            foreach (var s in scored)
            {
                result.ByPlayerId[s.PlayerId] = new RecommendedLineupPlayer { ExpectedPoints = s.ExpectedPoints };
            }
            return result;
        }

        var (bestDef, bestMid, bestFwd, _) = best.Value;
        result.Formation = $"{bestDef}-{bestMid}-{bestFwd}";

        var startingIds = new HashSet<int> { byType[GoalkeeperType][0].PlayerId };
        startingIds.UnionWith(byType[DefenderType].Take(bestDef).Select(p => p.PlayerId));
        startingIds.UnionWith(byType[MidfielderType].Take(bestMid).Select(p => p.PlayerId));
        startingIds.UnionWith(byType[ForwardType].Take(bestFwd).Select(p => p.PlayerId));

        var startingRanked = scored
            .Where(s => startingIds.Contains(s.PlayerId))
            .OrderByDescending(s => s.ExpectedPoints)
            .ToList();
        var captainId = startingRanked.Count > 0 ? startingRanked[0].PlayerId : (int?)null;
        var viceCaptainId = startingRanked.Count > 1 ? startingRanked[1].PlayerId : (int?)null;

        foreach (var s in scored)
        {
            result.ByPlayerId[s.PlayerId] = new RecommendedLineupPlayer
            {
                IsStarting = startingIds.Contains(s.PlayerId),
                IsCaptain = s.PlayerId == captainId,
                IsViceCaptain = s.PlayerId == viceCaptainId,
                ExpectedPoints = Math.Round(s.ExpectedPoints, 2),
            };
        }

        return result;
    }

    private static List<(int PlayerId, double ExpectedPoints)> RankedByType(
        List<(int PlayerId, int ElementType, double ExpectedPoints)> scored, int elementType)
        => scored
            .Where(s => s.ElementType == elementType)
            .OrderByDescending(s => s.ExpectedPoints)
            .Select(s => (s.PlayerId, s.ExpectedPoints))
            .ToList();

    private static double ExpectedPointsFor(Player player, List<Fixture> eventFixtures)
    {
        var teamFixtures = eventFixtures.Where(f => f.TeamH == player.Team || f.TeamA == player.Team);

        var form = ParseDecimal(player.Form);
        var effectiveForm = form > 0 ? form : ParseDecimal(player.PointsPerGame);

        double expected = 0;
        foreach (var fixture in teamFixtures)
        {
            var isHome = fixture.TeamH == player.Team;
            var difficulty = isHome ? fixture.TeamHDifficulty : fixture.TeamADifficulty;
            var fixtureFactor = (6.0 - difficulty) / 3.0;
            expected += effectiveForm * fixtureFactor;
        }

        return player.Status == "a" ? expected : expected + UnavailablePenalty;
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
