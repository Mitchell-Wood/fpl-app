using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Builds a fresh 15-man squad from scratch under a budget — for a Wildcard or Free Hit, where a
/// manager can pick any legal squad rather than making incremental transfers. Also picks the
/// best-scoring valid starting XI and captain from the resulting 15.
/// </summary>
public class SquadBuilderService
{
    // FPL squad composition rules: 15 players (2 GK, 5 DEF, 5 MID, 3 FWD), max 3 from any one
    // real-world team. Unchanged for many seasons.
    private static readonly IReadOnlyDictionary<int, int> SquadQuotas = new Dictionary<int, int>
    {
        [1] = 2, // GK
        [2] = 5, // DEF
        [3] = 5, // MID
        [4] = 3, // FWD
    };

    /// <param name="budget">Total to spend, in tenths of a million (e.g. 1000 = £100.0m).</param>
    /// <param name="fixtureLookaheadWeeks">
    /// How many upcoming gameweeks to project points over — 1 for a Free Hit (a single-week
    /// squad), or however many gameweeks until the next squad change for a Wildcard.
    /// </param>
    /// <param name="maxPerTeam">FPL's per-team squad cap (3), exposed for testability.</param>
    public SquadBuildResult BuildSquad(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        int budget,
        int fixtureLookaheadWeeks = 1,
        int maxPerTeam = 3)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);

        var rawDifficultyByTeam = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, fixtureLookaheadWeeks);

        var byPosition = bootstrap.Elements
            .Where(p => p.Status == "a")
            .GroupBy(p => p.ElementType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var (selected, teamCounts) = BuildCheapestFeasibleSquad(byPosition, maxPerTeam);

        var floorCost = selected.Sum(p => p.NowCost);
        if (floorCost > budget)
        {
            throw new InvalidOperationException(
                $"A budget of £{budget / 10.0:0.0}m isn't enough to field a valid squad — the cheapest legal squad costs £{floorCost / 10.0:0.0}m.");
        }

        var remainingBudget = budget - floorCost;
        SpendRemainingBudgetOnUpgrades(selected, teamCounts, byPosition, rawDifficultyByTeam, maxPerTeam, ref remainingBudget);

        return BuildResult(bootstrap, selected, budget, remainingBudget, rawDifficultyByTeam);
    }

    /// <summary>
    /// Fills every position quota with the cheapest available players, skipping any that would
    /// breach the per-team cap — establishing a guaranteed-feasible, minimum-cost starting point
    /// to spend the rest of the budget upgrading from.
    /// </summary>
    private static (List<Player> Selected, Dictionary<int, int> TeamCounts) BuildCheapestFeasibleSquad(
        IReadOnlyDictionary<int, List<Player>> byPosition, int maxPerTeam)
    {
        var selected = new List<Player>();
        var teamCounts = new Dictionary<int, int>();

        foreach (var (elementType, quota) in SquadQuotas)
        {
            var pool = byPosition.GetValueOrDefault(elementType, []).OrderBy(p => p.NowCost).ToList();
            var filled = 0;

            foreach (var player in pool)
            {
                if (filled == quota)
                {
                    break;
                }
                if (teamCounts.GetValueOrDefault(player.Team) >= maxPerTeam)
                {
                    continue;
                }

                selected.Add(player);
                teamCounts[player.Team] = teamCounts.GetValueOrDefault(player.Team) + 1;
                filled++;
            }

            if (filled < quota)
            {
                throw new InvalidOperationException($"Not enough eligible players to fill {quota} slots for position {elementType}.");
            }
        }

        return (selected, teamCounts);
    }

    /// <summary>
    /// Repeatedly swaps in the single biggest projected-points upgrade that still fits the
    /// remaining budget and team cap, until no affordable improvement is left.
    /// </summary>
    private static void SpendRemainingBudgetOnUpgrades(
        List<Player> selected,
        Dictionary<int, int> teamCounts,
        IReadOnlyDictionary<int, List<Player>> byPosition,
        IReadOnlyDictionary<int, List<int>> rawDifficultyByTeam,
        int maxPerTeam,
        ref int remainingBudget)
    {
        var selectedIds = selected.Select(p => p.Id).ToHashSet();

        // Each accepted swap strictly increases total projected points, and the player pool is
        // finite, so this always terminates; the cap is just a defensive backstop.
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            Player? bestOut = null;
            Player? bestIn = null;
            var bestGain = 0.0;

            foreach (var current in selected)
            {
                foreach (var candidate in byPosition.GetValueOrDefault(current.ElementType, []))
                {
                    if (selectedIds.Contains(candidate.Id))
                    {
                        continue;
                    }

                    var costDelta = candidate.NowCost - current.NowCost;
                    if (costDelta > remainingBudget)
                    {
                        continue;
                    }

                    var isSameTeam = candidate.Team == current.Team;
                    if (!isSameTeam && teamCounts.GetValueOrDefault(candidate.Team) >= maxPerTeam)
                    {
                        continue;
                    }

                    var gain = PlayerProjection.EstimateProjectedPoints(candidate, rawDifficultyByTeam)
                        - PlayerProjection.EstimateProjectedPoints(current, rawDifficultyByTeam);
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestOut = current;
                        bestIn = candidate;
                    }
                }
            }

            if (bestOut is null || bestIn is null)
            {
                break;
            }

            var index = selected.IndexOf(bestOut);
            selected[index] = bestIn;
            selectedIds.Remove(bestOut.Id);
            selectedIds.Add(bestIn.Id);
            teamCounts[bestOut.Team]--;
            teamCounts[bestIn.Team] = teamCounts.GetValueOrDefault(bestIn.Team) + 1;
            remainingBudget -= bestIn.NowCost - bestOut.NowCost;
        }
    }

    /// <summary>
    /// Picks the best-scoring valid formation (3-5 DEF, 2-5 MID, 1-3 FWD, summing to 10 outfield
    /// players) for the starting XI, plus captain (the single highest projected scorer in it).
    /// </summary>
    private static SquadBuildResult BuildResult(
        BootstrapStatic bootstrap,
        List<Player> selected,
        int budget,
        int remainingBudget,
        IReadOnlyDictionary<int, List<int>> rawDifficultyByTeam)
    {
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var projected = selected.ToDictionary(p => p.Id, p => PlayerProjection.EstimateProjectedPoints(p, rawDifficultyByTeam));

        var selectedByPosition = selected
            .GroupBy(p => p.ElementType)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => projected[p.Id]).ToList());

        var goalkeepers = selectedByPosition.GetValueOrDefault(1, []);
        var defenders = selectedByPosition.GetValueOrDefault(2, []);
        var midfielders = selectedByPosition.GetValueOrDefault(3, []);
        var forwards = selectedByPosition.GetValueOrDefault(4, []);

        var bestFormation = (Def: 0, Mid: 0, Fwd: 0);
        var bestXiScore = -1.0;

        for (var def = 3; def <= 5; def++)
        {
            for (var mid = 2; mid <= 5; mid++)
            {
                var fwd = 10 - def - mid;
                if (fwd is < 1 or > 3)
                {
                    continue;
                }

                var score = TopScore(goalkeepers, 1, projected) + TopScore(defenders, def, projected)
                    + TopScore(midfielders, mid, projected) + TopScore(forwards, fwd, projected);
                if (score > bestXiScore)
                {
                    bestXiScore = score;
                    bestFormation = (def, mid, fwd);
                }
            }
        }

        var startingIds = goalkeepers.Take(1)
            .Concat(defenders.Take(bestFormation.Def))
            .Concat(midfielders.Take(bestFormation.Mid))
            .Concat(forwards.Take(bestFormation.Fwd))
            .Select(p => p.Id)
            .ToHashSet();

        var captainId = startingIds.OrderByDescending(id => projected[id]).First();

        var players = selected
            .OrderBy(p => p.ElementType)
            .ThenByDescending(p => projected[p.Id])
            .Select(p => new SquadBuilderPlayer
            {
                PlayerId = p.Id,
                WebName = p.WebName,
                TeamName = teamsById.GetValueOrDefault(p.Team)?.ShortName ?? "?",
                ElementType = p.ElementType,
                NowCost = p.NowCost,
                ProjectedPoints = Math.Round(projected[p.Id], 2),
                IsStarting = startingIds.Contains(p.Id),
                IsCaptain = p.Id == captainId,
            })
            .ToList();

        return new SquadBuildResult
        {
            Budget = budget,
            TotalCost = budget - remainingBudget,
            BudgetRemaining = remainingBudget,
            Formation = $"{bestFormation.Def}-{bestFormation.Mid}-{bestFormation.Fwd}",
            StartingElevenProjectedPoints = Math.Round(bestXiScore, 2),
            Players = players,
        };
    }

    private static double TopScore(List<Player> playersDescending, int count, Dictionary<int, double> projected)
        => playersDescending.Take(count).Sum(p => projected[p.Id]);
}
