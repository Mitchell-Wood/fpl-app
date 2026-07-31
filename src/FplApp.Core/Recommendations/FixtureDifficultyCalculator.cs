using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

public static class FixtureDifficultyCalculator
{
    /// <summary>Average fixture difficulty per team over the next N unplayed gameweeks.</summary>
    public static Dictionary<int, double> AverageUpcomingDifficultyByTeam(IReadOnlyList<Fixture> fixtures, int lookaheadWeeks)
        => RawUpcomingDifficultiesByTeam(fixtures, lookaheadWeeks).ToDictionary(kv => kv.Key, kv => kv.Value.Average());

    /// <summary>
    /// Each team's per-fixture difficulty over the next N unplayed gameweeks, left unaveraged so
    /// callers can sum a per-fixture contribution instead — naturally yielding nothing for a blank
    /// gameweek and counting twice for a double.
    /// </summary>
    public static Dictionary<int, List<int>> RawUpcomingDifficultiesByTeam(IReadOnlyList<Fixture> fixtures, int lookaheadWeeks)
    {
        var nextEvent = fixtures
            .Where(f => !f.Finished && f.Event.HasValue)
            .Select(f => f.Event!.Value)
            .DefaultIfEmpty()
            .Min();

        if (nextEvent == 0)
        {
            return [];
        }

        var lastEvent = nextEvent + lookaheadWeeks - 1;
        return BuildRawDifficulties(fixtures.Where(f => f.Event is { } eventId && eventId >= nextEvent && eventId <= lastEvent));
    }

    /// <summary>
    /// Each team's per-fixture difficulty for one specific gameweek — like
    /// <see cref="RawUpcomingDifficultiesByTeam"/> but for a single chosen event (e.g. a candidate
    /// Bench Boost week) rather than a lookahead window from the next unplayed gameweek. A team
    /// with no fixture that week is simply absent; a double gameweek yields both fixtures.
    /// </summary>
    public static Dictionary<int, List<int>> RawDifficultiesForEvent(IReadOnlyList<Fixture> fixtures, int eventId)
        => BuildRawDifficulties(fixtures.Where(f => f.Event == eventId));

    private static Dictionary<int, List<int>> BuildRawDifficulties(IEnumerable<Fixture> fixtures)
    {
        var difficultiesByTeam = new Dictionary<int, List<int>>();

        void AddDifficulty(int teamId, int difficulty)
        {
            if (!difficultiesByTeam.TryGetValue(teamId, out var list))
            {
                list = [];
                difficultiesByTeam[teamId] = list;
            }
            list.Add(difficulty);
        }

        foreach (var fixture in fixtures)
        {
            AddDifficulty(fixture.TeamH, fixture.TeamHDifficulty);
            AddDifficulty(fixture.TeamA, fixture.TeamADifficulty);
        }

        return difficultiesByTeam;
    }
}
