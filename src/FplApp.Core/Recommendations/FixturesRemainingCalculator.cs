using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

public static class FixturesRemainingCalculator
{
    private const string BenchBoostChipName = "bboost";
    private const int GoalkeeperType = 1;
    private const int DefenderType = 2;
    private const int MidfielderType = 3;
    private const int ForwardType = 4;

    private const int MinDefenders = 3;
    private const int MaxDefenders = 5;
    private const int MinMidfielders = 2;
    private const int MaxMidfielders = 5;
    private const int MinForwards = 1;
    private const int MaxForwards = 3;

    /// <summary>
    /// Counts how many fixture-legs a manager's effective squad still has left to play in the given
    /// gameweek. Normally only the starting XI (position 1-11) counts, since the bench doesn't
    /// score — but with Bench Boost active the whole 15 counts, since the bench scores too that
    /// gameweek. A starter whose fixture(s) have all finished with zero minutes played is treated as
    /// confirmed not to play, and is auto-substituted for the next eligible bench player (mirroring
    /// FPL's own auto-substitution rules), so a not-yet-started bench player counts in their place. A
    /// player whose team has two fixtures (a double gameweek) counts twice; a player whose team
    /// doesn't play at all counts zero.
    /// </summary>
    public static int CountRemaining(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        TeamPicks picks,
        int eventId,
        IReadOnlyDictionary<int, int> minutesByElementId)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(minutesByElementId);

        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);

        // FPL leaves "finished" false for a while after full-time, pending official confirmation
        // (bonus points etc.) — "finished_provisional" flips true immediately at the final whistle,
        // so it's the accurate signal for whether a fixture is actually still to be played.
        var eventFixtures = fixtures.Where(f => f.Event == eventId && !f.FinishedProvisional).ToList();
        var allEventFixtures = fixtures.Where(f => f.Event == eventId).ToList();

        int RemainingFor(Player player)
            => eventFixtures.Count(f => f.TeamH == player.Team || f.TeamA == player.Team);

        bool IsConfirmedOut(Player player)
        {
            var teamFixtures = allEventFixtures.Where(f => f.TeamH == player.Team || f.TeamA == player.Team);
            var minutes = minutesByElementId.GetValueOrDefault(player.Id, 0);
            return minutes == 0 && teamFixtures.All(f => f.FinishedProvisional);
        }

        if (picks.ActiveChip == BenchBoostChipName)
        {
            var count = 0;
            foreach (var pick in picks.Picks)
            {
                if (playersById.TryGetValue(pick.Element, out var player))
                {
                    count += RemainingFor(player);
                }
            }

            return count;
        }

        var startingXi = picks.Picks.Where(p => p.Position <= 11).OrderBy(p => p.Position).ToList();
        var bench = picks.Picks.Where(p => p.Position > 11).OrderBy(p => p.Position).ToList();

        // The players actually "in" the effective XI after auto-subs, keyed by their original slot.
        var effectiveXi = new List<Pick>(startingXi);
        var usedBenchElements = new HashSet<int>();

        int CountByType(int elementType)
            => effectiveXi.Count(p => playersById.TryGetValue(p.Element, out var pl) && pl.ElementType == elementType);

        bool WouldBeValidFormation(int outgoingType, int incomingType)
        {
            var defenders = CountByType(DefenderType) + (outgoingType == DefenderType ? -1 : 0) + (incomingType == DefenderType ? 1 : 0);
            var midfielders = CountByType(MidfielderType) + (outgoingType == MidfielderType ? -1 : 0) + (incomingType == MidfielderType ? 1 : 0);
            var forwards = CountByType(ForwardType) + (outgoingType == ForwardType ? -1 : 0) + (incomingType == ForwardType ? 1 : 0);
            return defenders >= MinDefenders && defenders <= MaxDefenders
                && midfielders >= MinMidfielders && midfielders <= MaxMidfielders
                && forwards >= MinForwards && forwards <= MaxForwards;
        }

        // Goalkeeper sub: exactly one reserve GK, swaps in if the starting GK is confirmed out.
        var starterGkIndex = effectiveXi.FindIndex(p => playersById.TryGetValue(p.Element, out var pl) && pl.ElementType == GoalkeeperType);
        if (starterGkIndex >= 0 && playersById.TryGetValue(effectiveXi[starterGkIndex].Element, out var starterGk) && IsConfirmedOut(starterGk))
        {
            var reserveGk = bench.FirstOrDefault(p => playersById.TryGetValue(p.Element, out var pl) && pl.ElementType == GoalkeeperType);
            if (reserveGk is not null)
            {
                effectiveXi[starterGkIndex] = reserveGk;
                usedBenchElements.Add(reserveGk.Element);
            }
        }

        // Outfield subs: bench players in order, each filling the earliest still-confirmed-out
        // starter whose replacement keeps a legal formation. A slot that's already been filled by a
        // substitute is locked — FPL doesn't chain a second substitution on top of the first even if
        // the substitute themselves ends up not playing.
        var lockedSlots = new bool[effectiveXi.Count];
        foreach (var reserve in bench)
        {
            if (usedBenchElements.Contains(reserve.Element) || !playersById.TryGetValue(reserve.Element, out var reservePlayer))
            {
                continue;
            }
            if (reservePlayer.ElementType == GoalkeeperType)
            {
                continue;
            }

            for (var i = 0; i < effectiveXi.Count; i++)
            {
                if (lockedSlots[i] || !playersById.TryGetValue(effectiveXi[i].Element, out var incumbent) || incumbent.ElementType == GoalkeeperType)
                {
                    continue;
                }
                if (!IsConfirmedOut(incumbent))
                {
                    continue;
                }
                if (!WouldBeValidFormation(incumbent.ElementType, reservePlayer.ElementType))
                {
                    continue;
                }

                effectiveXi[i] = reserve;
                lockedSlots[i] = true;
                usedBenchElements.Add(reserve.Element);
                break;
            }
        }

        var total = 0;
        foreach (var pick in effectiveXi)
        {
            if (playersById.TryGetValue(pick.Element, out var player))
            {
                total += RemainingFor(player);
            }
        }

        return total;
    }
}
