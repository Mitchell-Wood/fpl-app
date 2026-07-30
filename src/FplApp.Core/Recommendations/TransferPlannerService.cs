using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Pairs squad players with affordable, same-position replacement candidates, and works out how
/// many of a manager's free transfers are actually worth using.
/// </summary>
public class TransferPlannerService
{
    private readonly PlayerRecommendationService _recommendationService;

    public TransferPlannerService(PlayerRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    private sealed record CandidateSuggestion(SquadPickAnalysis Pick, IReadOnlyList<Player> Candidates, int Budget, double PointsGain);

    /// <summary>
    /// Considers every squad player (not just ones flagged for injury/form/fixtures) and scores a
    /// same-position, affordable replacement for each wherever one genuinely outscores the current
    /// player — so a strong squad's weakest links surface regardless of position. Ranked by
    /// projected points gained over the fixture lookahead window, biggest first.
    /// </summary>
    private List<CandidateSuggestion> ComputeCandidateSuggestions(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<SquadPickAnalysis> squad,
        int bank,
        int fixtureLookaheadWeeks,
        int candidatesPerPlayer)
    {
        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var ownedPlayerIds = squad.Select(p => p.PlayerId).ToHashSet();
        var difficultyByTeam = FixtureDifficultyCalculator.AverageUpcomingDifficultyByTeam(fixtures, fixtureLookaheadWeeks);
        var rawDifficultyByTeam = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, fixtureLookaheadWeeks);

        var results = new List<CandidateSuggestion>();

        foreach (var pick in squad)
        {
            if (!playersById.TryGetValue(pick.PlayerId, out var currentPlayer))
            {
                continue;
            }

            var budget = bank + pick.NowCost;
            var candidates = _recommendationService.RecommendPlayers(
                bootstrap, fixtures, pick.ElementType, candidatesPerPlayer, fixtureLookaheadWeeks,
                ownedPlayerIds, budget);
            if (candidates.Count == 0)
            {
                continue;
            }

            // Score picks which candidate is the best overall fit (form, expected points, value,
            // fixtures combined); only worth suggesting at all if that candidate beats the current
            // player, otherwise every player in the squad would show upgrade noise.
            var scoreGain = PlayerRecommendationService.Score(candidates[0], difficultyByTeam)
                - PlayerRecommendationService.Score(currentPlayer, difficultyByTeam);
            if (scoreGain <= 0)
            {
                continue;
            }

            // Points gain is the user-facing "how good is this transfer" number and what ranks and
            // filters suggestions, since it's directly comparable to the cost of a -4 hit.
            var pointsGain = EstimateProjectedPoints(candidates[0], rawDifficultyByTeam)
                - EstimateProjectedPoints(currentPlayer, rawDifficultyByTeam);
            if (pointsGain <= 0)
            {
                continue;
            }

            results.Add(new CandidateSuggestion(pick, candidates, budget, pointsGain));
        }

        return results.OrderByDescending(s => s.PointsGain).ToList();
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

    /// <param name="maxSuggestions">Caps how many out-players are suggested, keeping the list to the biggest real upgrades.</param>
    public IReadOnlyList<TransferSuggestion> SuggestTransfers(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<SquadPickAnalysis> squad,
        int bank,
        int fixtureLookaheadWeeks = 5,
        int candidatesPerPlayer = 3,
        int maxSuggestions = 5)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(squad);

        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var candidates = ComputeCandidateSuggestions(bootstrap, fixtures, squad, bank, fixtureLookaheadWeeks, candidatesPerPlayer);
        return candidates.Take(maxSuggestions).Select(s => MapSuggestion(s, teamsById)).ToList();
    }

    /// <summary>
    /// Works out how many of your free transfers are actually worth using this week. Greedily takes
    /// the biggest-points-gain transfers first as long as they fit a shared budget pool (bank plus
    /// proceeds from transfers already picked), up to the number of free transfers available —
    /// anything left over is better banked for future flexibility (an injury, or a bigger rebuild a
    /// few weeks out) than spent on a marginal upgrade. Also flags the next-best transfer beyond
    /// your free ones in case it's worth a -4 hit.
    /// </summary>
    public TransferPlanResult BuildTransferPlan(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<SquadPickAnalysis> squad,
        int bank,
        int fixtureLookaheadWeeks,
        int freeTransfersAvailable,
        int candidatesPerPlayer = 3)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(squad);

        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var ranked = ComputeCandidateSuggestions(bootstrap, fixtures, squad, bank, fixtureLookaheadWeeks, candidatesPerPlayer);

        var result = new TransferPlanResult { FreeTransfersAvailable = freeTransfersAvailable };
        var pool = bank; // shared budget: bank plus (or minus) each transfer's own proceeds as they're taken

        foreach (var candidate in ranked)
        {
            var bestIn = candidate.Candidates[0];
            var netCost = bestIn.NowCost - candidate.Pick.NowCost; // negative when the swap frees up money

            if (netCost > pool)
            {
                continue; // doesn't fit what's left of the shared pool
            }

            if (result.RecommendedTransfers.Count < freeTransfersAvailable)
            {
                pool -= netCost;
                result.RecommendedTransfers.Add(MapSuggestion(candidate, teamsById));
                result.TotalExpectedPointsGain += candidate.PointsGain;
            }
            else if (result.HitCandidate is null)
            {
                // The single best transfer beyond your free ones — offered as a "worth a hit?"
                // option, evaluated independently of the pool since it's an extra transfer on top.
                result.HitCandidate = MapSuggestion(candidate, teamsById);
                result.HitCandidateNetGain = Math.Round(candidate.PointsGain - 4, 2);
                result.HitWorthIt = candidate.PointsGain > 4;
                break;
            }
        }

        result.FreeTransfersUsed = result.RecommendedTransfers.Count;
        result.FreeTransfersToBank = freeTransfersAvailable - result.FreeTransfersUsed;
        result.TotalExpectedPointsGain = Math.Round(result.TotalExpectedPointsGain, 2);

        return result;
    }

    /// <summary>
    /// Looks for a two-transfer plan: sell one squad player for a slightly cheaper same-position
    /// replacement (freeing up money), then put that money — plus bank — toward a bigger upgrade
    /// on a different squad player than their own price alone could afford. Returns the single
    /// best such plan (by combined score gain across both legs), or null if no combo beats what's
    /// already achievable with standalone transfers.
    /// </summary>
    public FundedUpgradeSuggestion? SuggestFundedUpgrade(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<SquadPickAnalysis> squad,
        int bank,
        int fixtureLookaheadWeeks = 5)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(squad);

        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var ownedPlayerIds = squad.Select(p => p.PlayerId).ToHashSet();
        var difficultyByTeam = FixtureDifficultyCalculator.AverageUpcomingDifficultyByTeam(fixtures, fixtureLookaheadWeeks);
        var rawDifficultyByTeam = FixtureDifficultyCalculator.RawUpcomingDifficultiesByTeam(fixtures, fixtureLookaheadWeeks);

        FundedUpgradeSuggestion? best = null;
        var bestNetGain = 0.0;

        foreach (var downgradeFrom in squad)
        {
            if (!playersById.TryGetValue(downgradeFrom.PlayerId, out var downgradeFromPlayer))
            {
                continue;
            }

            // The best-scoring replacement that costs strictly less than the current player —
            // i.e. a "slight downgrade" that frees up money rather than a random cheap bench option.
            var downgradeCandidates = _recommendationService.RecommendPlayers(
                bootstrap, fixtures, downgradeFrom.ElementType, 1, fixtureLookaheadWeeks,
                ownedPlayerIds, downgradeFrom.NowCost - 1);
            if (downgradeCandidates.Count == 0)
            {
                continue;
            }

            var downgradeTo = downgradeCandidates[0];
            var moneySaved = downgradeFrom.NowCost - downgradeTo.NowCost;
            if (moneySaved <= 0)
            {
                continue;
            }

            var downgradeScoreDelta = PlayerRecommendationService.Score(downgradeTo, difficultyByTeam)
                - PlayerRecommendationService.Score(downgradeFromPlayer, difficultyByTeam);

            foreach (var upgradeFrom in squad)
            {
                if (upgradeFrom.PlayerId == downgradeFrom.PlayerId)
                {
                    continue;
                }
                if (!playersById.TryGetValue(upgradeFrom.PlayerId, out var upgradeFromPlayer))
                {
                    continue;
                }

                var standaloneBudget = bank + upgradeFrom.NowCost;
                var fundedBudget = standaloneBudget + moneySaved;

                var fundedCandidates = _recommendationService.RecommendPlayers(
                    bootstrap, fixtures, upgradeFrom.ElementType, 1, fixtureLookaheadWeeks,
                    ownedPlayerIds, fundedBudget);
                if (fundedCandidates.Count == 0)
                {
                    continue;
                }

                var upgradeTo = fundedCandidates[0];

                // Only interesting if the extra cash from the downgrade is what actually unlocks
                // this player — i.e. they weren't already affordable on the player's own budget.
                if (upgradeTo.NowCost <= standaloneBudget)
                {
                    continue;
                }

                var upgradeScoreDelta = PlayerRecommendationService.Score(upgradeTo, difficultyByTeam)
                    - PlayerRecommendationService.Score(upgradeFromPlayer, difficultyByTeam);

                var netGain = downgradeScoreDelta + upgradeScoreDelta;
                if (netGain <= bestNetGain)
                {
                    continue;
                }

                bestNetGain = netGain;
                var netPointsGain = (EstimateProjectedPoints(downgradeTo, rawDifficultyByTeam) - EstimateProjectedPoints(downgradeFromPlayer, rawDifficultyByTeam))
                    + (EstimateProjectedPoints(upgradeTo, rawDifficultyByTeam) - EstimateProjectedPoints(upgradeFromPlayer, rawDifficultyByTeam));

                best = new FundedUpgradeSuggestion
                {
                    MoneySaved = moneySaved,
                    NetExpectedPointsGain = Math.Round(netPointsGain, 2),
                    Downgrade = new TransferLeg
                    {
                        OutPlayerId = downgradeFrom.PlayerId,
                        OutWebName = downgradeFrom.WebName,
                        OutTeamName = downgradeFrom.TeamName,
                        OutNowCost = downgradeFrom.NowCost,
                        InPlayerId = downgradeTo.Id,
                        InWebName = downgradeTo.WebName,
                        InTeamName = teamsById.GetValueOrDefault(downgradeTo.Team)?.ShortName ?? "?",
                        InNowCost = downgradeTo.NowCost,
                        InForm = ParseDecimal(downgradeTo.Form),
                        InTotalPoints = downgradeTo.TotalPoints,
                    },
                    Upgrade = new TransferLeg
                    {
                        OutPlayerId = upgradeFrom.PlayerId,
                        OutWebName = upgradeFrom.WebName,
                        OutTeamName = upgradeFrom.TeamName,
                        OutNowCost = upgradeFrom.NowCost,
                        InPlayerId = upgradeTo.Id,
                        InWebName = upgradeTo.WebName,
                        InTeamName = teamsById.GetValueOrDefault(upgradeTo.Team)?.ShortName ?? "?",
                        InNowCost = upgradeTo.NowCost,
                        InForm = ParseDecimal(upgradeTo.Form),
                        InTotalPoints = upgradeTo.TotalPoints,
                    },
                };
            }
        }

        return best;
    }

    /// <summary>
    /// Projects a player's points over the fixture lookahead window: recent form (or FPL's own
    /// expected-points-next-gameweek, or season points-per-game, whichever is the best signal
    /// available) applied to each upcoming fixture and scaled by that fixture's difficulty. Summing
    /// per-fixture — rather than using an average — means a blank gameweek contributes nothing and
    /// a double gameweek counts twice, automatically.
    /// </summary>
    private static double EstimateProjectedPoints(Player player, IReadOnlyDictionary<int, List<int>> rawDifficultiesByTeam)
    {
        var form = ParseDecimal(player.Form);
        var expectedPointsNext = ParseDecimal(player.ExpectedPointsNext);
        var pointsPerGame = ParseDecimal(player.PointsPerGame);
        var effectiveRate = form > 0 ? form : (expectedPointsNext > 0 ? expectedPointsNext : pointsPerGame);

        if (!rawDifficultiesByTeam.TryGetValue(player.Team, out var difficulties) || difficulties.Count == 0)
        {
            return 0;
        }

        return difficulties.Sum(difficulty => effectiveRate * ((6.0 - difficulty) / 3.0));
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
