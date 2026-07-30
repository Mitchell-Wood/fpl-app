using System.Globalization;
using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// Pairs squad players already flagged by <see cref="SquadAnalysisService"/> (injured, poor form,
/// tough fixtures) with affordable, same-position replacement candidates.
/// </summary>
public class TransferPlannerService
{
    private readonly PlayerRecommendationService _recommendationService;

    public TransferPlannerService(PlayerRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    /// <summary>
    /// Considers every squad player (not just ones already flagged for injury/form/fixtures) and
    /// suggests same-position, affordable replacements wherever one genuinely outscores the
    /// current player — so a strong squad's weakest links surface regardless of position, rather
    /// than only whichever players happen to be flagged.
    /// </summary>
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
        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var ownedPlayerIds = squad.Select(p => p.PlayerId).ToHashSet();
        var difficultyByTeam = FixtureDifficultyCalculator.AverageUpcomingDifficultyByTeam(fixtures, fixtureLookaheadWeeks);

        var scored = new List<(SquadPickAnalysis Pick, IReadOnlyList<Player> Candidates, int Budget, double ScoreGain)>();

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

            // Only worth suggesting if the best affordable candidate actually outscores the
            // current player — otherwise every player in the squad would show upgrade noise.
            var scoreGain = PlayerRecommendationService.Score(candidates[0], difficultyByTeam)
                - PlayerRecommendationService.Score(currentPlayer, difficultyByTeam);
            if (scoreGain <= 0)
            {
                continue;
            }

            scored.Add((pick, candidates, budget, scoreGain));
        }

        return scored
            .OrderByDescending(s => s.ScoreGain)
            .Take(maxSuggestions)
            .Select(s => new TransferSuggestion
            {
                OutPlayerId = s.Pick.PlayerId,
                OutWebName = s.Pick.WebName,
                OutTeamName = s.Pick.TeamName,
                OutFlags = s.Pick.Flags,
                BudgetAvailable = s.Budget,
                Candidates = s.Candidates.Select(c => new TransferCandidate
                {
                    PlayerId = c.Id,
                    WebName = c.WebName,
                    TeamName = teamsById.GetValueOrDefault(c.Team)?.ShortName ?? "?",
                    NowCost = c.NowCost,
                    Form = ParseDecimal(c.Form),
                    TotalPoints = c.TotalPoints,
                }).ToList(),
            })
            .ToList();
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
                best = new FundedUpgradeSuggestion
                {
                    MoneySaved = moneySaved,
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

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
