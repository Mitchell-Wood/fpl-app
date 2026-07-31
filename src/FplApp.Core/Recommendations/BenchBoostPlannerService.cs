using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Plans transfers to maximize your full 15-man squad's points for one specific gameweek — the
/// metric that matters when Bench Boost is active, since every player scores that week, not just
/// your starting XI. Unlike <see cref="TransferPlannerService"/> (which projects over a multi-week
/// lookahead window), every player here is scored for that single target gameweek only, and every
/// squad player is considered, including your current bench — normally low priority for a
/// transfer, but exactly what Bench Boost cashes in on.
/// </summary>
public class BenchBoostPlannerService
{
    private sealed record CandidateSuggestion(SquadPickAnalysis Pick, IReadOnlyList<Player> Candidates, int Budget, double PointsGain);

    /// <summary>
    /// Ranks every squad player's best affordable same-position replacement by points gained for
    /// the target gameweek, then greedily builds a plan using up to
    /// <paramref name="freeTransfersAvailable"/> free transfers within a shared budget pool (bank
    /// plus proceeds from transfers already picked) — mirroring
    /// <see cref="TransferPlannerService.BuildTransferPlan"/>'s greedy/hit-candidate logic, but
    /// scored for one gameweek instead of a multi-week average.
    /// </summary>
    public BenchBoostPlanResult PlanBenchBoost(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<SquadPickAnalysis> squad,
        int bank,
        int eventId,
        int freeTransfersAvailable,
        int candidatesPerPlayer = 3)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(squad);

        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var ownedPlayerIds = squad.Select(p => p.PlayerId).ToHashSet();
        var eventDifficultyByTeam = FixtureDifficultyCalculator.RawDifficultiesForEvent(fixtures, eventId);

        var currentSquadPoints = 0.0;
        var ranked = new List<CandidateSuggestion>();

        foreach (var pick in squad)
        {
            if (!playersById.TryGetValue(pick.PlayerId, out var currentPlayer))
            {
                continue;
            }

            var currentPoints = PlayerProjection.EstimateProjectedPoints(currentPlayer, eventDifficultyByTeam);
            currentSquadPoints += currentPoints;

            var budget = bank + pick.NowCost;
            var candidates = bootstrap.Elements
                .Where(p => p.Status == "a")
                .Where(p => p.ElementType == pick.ElementType)
                .Where(p => !ownedPlayerIds.Contains(p.Id))
                .Where(p => p.NowCost <= budget)
                .OrderByDescending(p => PlayerProjection.EstimateProjectedPoints(p, eventDifficultyByTeam))
                .Take(candidatesPerPlayer)
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            var pointsGain = PlayerProjection.EstimateProjectedPoints(candidates[0], eventDifficultyByTeam) - currentPoints;
            ranked.Add(new CandidateSuggestion(pick, candidates, budget, pointsGain));
        }

        ranked = ranked.OrderByDescending(c => c.PointsGain).ToList();

        var plan = new TransferPlanResult { FreeTransfersAvailable = freeTransfersAvailable };
        var pool = bank; // shared budget: bank plus (or minus) each transfer's own proceeds as they're taken

        foreach (var candidate in ranked)
        {
            if (candidate.PointsGain <= 0)
            {
                continue; // never recommend a transfer projected to lose points, even a free one
            }

            var bestIn = candidate.Candidates[0];
            var netCost = bestIn.NowCost - candidate.Pick.NowCost;

            if (netCost > pool)
            {
                continue; // doesn't fit what's left of the shared pool
            }

            if (plan.RecommendedTransfers.Count < freeTransfersAvailable)
            {
                pool -= netCost;
                plan.RecommendedTransfers.Add(MapSuggestion(candidate, teamsById));
                plan.TotalExpectedPointsGain += candidate.PointsGain;
            }
            else if (plan.HitCandidate is null)
            {
                plan.HitCandidate = MapSuggestion(candidate, teamsById);
                plan.HitCandidateNetGain = Math.Round(candidate.PointsGain - 4, 2);
                plan.HitWorthIt = candidate.PointsGain > 4;
                break;
            }
        }

        plan.FreeTransfersUsed = plan.RecommendedTransfers.Count;
        plan.FreeTransfersToBank = freeTransfersAvailable - plan.FreeTransfersUsed;
        plan.TotalExpectedPointsGain = Math.Round(plan.TotalExpectedPointsGain, 2);

        return new BenchBoostPlanResult
        {
            EventId = eventId,
            CurrentSquadProjectedPoints = Math.Round(currentSquadPoints, 2),
            ProjectedSquadPointsAfterTransfers = Math.Round(currentSquadPoints + plan.TotalExpectedPointsGain, 2),
            Plan = plan,
        };
    }

    private static TransferSuggestion MapSuggestion(CandidateSuggestion s, IReadOnlyDictionary<int, Team> teamsById)
        => new()
        {
            OutPlayerId = s.Pick.PlayerId,
            OutWebName = s.Pick.WebName,
            OutTeamName = s.Pick.TeamName,
            OutFlags = s.Pick.Flags,
            BudgetAvailable = s.Budget,
            ExpectedPointsGain = Math.Round(s.PointsGain, 2),
            Candidates = s.Candidates.Select(c => new TransferCandidate
            {
                PlayerId = c.Id,
                WebName = c.WebName,
                TeamName = teamsById.GetValueOrDefault(c.Team)?.ShortName ?? "?",
                NowCost = c.NowCost,
                Form = ParseDecimal(c.Form),
                TotalPoints = c.TotalPoints,
            }).ToList(),
        };

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
