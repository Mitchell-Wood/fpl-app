using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

public static class FixtureDifficultyCalculator
{
    /// <summary>Average fixture difficulty per team over the next N unplayed gameweeks.</summary>
    public static Dictionary<int, double> AverageUpcomingDifficultyByTeam(IReadOnlyList<Fixture> fixtures, int lookaheadWeeks)
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
            if (fixture.Event is not { } eventId || eventId < nextEvent || eventId > lastEvent)
            {
                continue;
            }

            AddDifficulty(fixture.TeamH, fixture.TeamHDifficulty);
            AddDifficulty(fixture.TeamA, fixture.TeamADifficulty);
        }

        return difficultiesByTeam.ToDictionary(kv => kv.Key, kv => kv.Value.Average());
    }
}
