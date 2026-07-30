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

    public IReadOnlyList<TransferSuggestion> SuggestTransfers(
        BootstrapStatic bootstrap,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<SquadPickAnalysis> squad,
        int bank,
        int fixtureLookaheadWeeks = 5,
        int candidatesPerPlayer = 3)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(squad);

        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var ownedPlayerIds = squad.Select(p => p.PlayerId).ToHashSet();

        var results = new List<TransferSuggestion>();

        foreach (var pick in squad.Where(p => p.Flags.Count > 0))
        {
            var budget = bank + pick.NowCost;
            var candidates = _recommendationService.RecommendPlayers(
                bootstrap, fixtures, pick.ElementType, candidatesPerPlayer, fixtureLookaheadWeeks,
                ownedPlayerIds, budget);

            results.Add(new TransferSuggestion
            {
                OutPlayerId = pick.PlayerId,
                OutWebName = pick.WebName,
                OutTeamName = pick.TeamName,
                OutFlags = pick.Flags,
                BudgetAvailable = budget,
                Candidates = candidates.Select(c => new TransferCandidate
                {
                    PlayerId = c.Id,
                    WebName = c.WebName,
                    TeamName = teamsById.GetValueOrDefault(c.Team)?.ShortName ?? "?",
                    NowCost = c.NowCost,
                    Form = ParseDecimal(c.Form),
                    TotalPoints = c.TotalPoints,
                }).ToList(),
            });
        }

        return results;
    }

    private static double ParseDecimal(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
