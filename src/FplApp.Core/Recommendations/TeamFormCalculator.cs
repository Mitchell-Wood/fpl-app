using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// One team's recent attack/defence strength, home/away split, as a factor centered on 1.0 (an
/// exactly average team scores/concedes at the long-run league rate) — the same scale
/// <see cref="ExpectedPointsEngine.FixtureFactor"/> already uses for its preseason strength ratio,
/// so the two can be blended directly.
/// </summary>
public readonly record struct TeamFormRating(double AttackHome, double AttackAway, double DefenceHome, double DefenceAway);

/// <summary>
/// Derives each team's recent-form strength from actual match results — distinct from
/// <see cref="Team.StrengthAttackHome"/> and friends, which are FPL's own preseason ratings and never
/// update from what's actually happened on the pitch. Feeds into
/// <see cref="ExpectedPointsEngine.FixtureFactor"/> as a third signal alongside FDR and preseason
/// strength, so a team on a genuine hot or cold streak is credited for it sooner than a
/// preseason-only rating would.
/// </summary>
public static class TeamFormCalculator
{
    // Long-run Premier League scoring averages, home vs away — the neutral baseline every observed
    // rate is normalized against (so an exactly average team's factor comes out at 1.0) and also the
    // shrinkage prior for a team with few recent games. Mirrors the equivalent constants used for the
    // clean-sheet-chance estimate on the frontend fixtures ticker.
    private const double PriorHomeGoals = 1.5;
    private const double PriorAwayGoals = 1.15;

    // Only a team's most recent games (at each venue) count toward its form — deliberately narrower
    // than a season-long average, since the whole point is to catch a team heating up or cooling down
    // that a season-to-date or preseason number would still be slow to reflect.
    private const int RollingWindowGames = 6;

    // Games played (at that venue) at which the observed rate and the neutral prior carry equal
    // weight — full prior at 0 games, mostly-observed once most of the rolling window is filled.
    private const double ShrinkageGames = 3;

    private static readonly double MinConcededRate = 0.1; // guards against a divide-by-zero for a team yet to concede

    /// <summary>Every team's current recent-form rating, keyed by team id. Empty before any fixture has finished.</summary>
    public static Dictionary<int, TeamFormRating> ComputeTeamForm(IReadOnlyList<Fixture> fixtures)
    {
        var homeGames = new Dictionary<int, List<(int For, int Against)>>();
        var awayGames = new Dictionary<int, List<(int For, int Against)>>();

        foreach (var fixture in fixtures.Where(f => f.Event.HasValue).OrderBy(f => f.Event))
        {
            if (!fixture.FinishedProvisional || fixture.TeamHScore is not { } homeScore || fixture.TeamAScore is not { } awayScore)
            {
                continue;
            }

            AddGame(homeGames, fixture.TeamH, homeScore, awayScore);
            AddGame(awayGames, fixture.TeamA, awayScore, homeScore);
        }

        var teamIds = homeGames.Keys.Concat(awayGames.Keys).Distinct();
        var ratings = new Dictionary<int, TeamFormRating>();
        foreach (var teamId in teamIds)
        {
            var home = Recent(homeGames.GetValueOrDefault(teamId, []));
            var away = Recent(awayGames.GetValueOrDefault(teamId, []));

            ratings[teamId] = new TeamFormRating(
                AttackHome: AttackFactor(home, PriorHomeGoals),
                AttackAway: AttackFactor(away, PriorAwayGoals),
                DefenceHome: DefenceFactor(home, PriorAwayGoals),
                DefenceAway: DefenceFactor(away, PriorHomeGoals));
        }

        return ratings;
    }

    private static void AddGame(Dictionary<int, List<(int For, int Against)>> byTeam, int teamId, int forGoals, int againstGoals)
    {
        if (!byTeam.TryGetValue(teamId, out var games))
        {
            games = [];
            byTeam[teamId] = games;
        }
        games.Add((forGoals, againstGoals));
    }

    private static List<(int For, int Against)> Recent(List<(int For, int Against)> chronologicalGames)
        => chronologicalGames.Count <= RollingWindowGames
            ? chronologicalGames
            : chronologicalGames[^RollingWindowGames..];

    private static double AttackFactor(List<(int For, int Against)> games, double prior)
        => ShrunkRate(games.Select(g => (double)g.For), prior) / prior;

    private static double DefenceFactor(List<(int For, int Against)> games, double opponentPrior)
        => opponentPrior / Math.Max(ShrunkRate(games.Select(g => (double)g.Against), opponentPrior), MinConcededRate);

    private static double ShrunkRate(IEnumerable<double> values, double prior)
    {
        var games = values.ToList();
        if (games.Count == 0)
        {
            return prior;
        }

        var observed = games.Average();
        var weight = games.Count / (games.Count + ShrinkageGames);
        return (weight * observed) + ((1 - weight) * prior);
    }
}
