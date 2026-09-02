using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

/// <summary>
/// One upcoming fixture's FPL difficulty rating for a team, plus who it's against and where — kept
/// alongside the difficulty (rather than averaged away) so callers can blend in the opponent's and
/// team's actual current-season strength via <see cref="ExpectedPointsEngine.FixtureFactor"/>.
/// <see cref="EventId"/> is the actual gameweek number, kept so callers projecting across a multi-week
/// window can tell how many gameweeks out a fixture is (e.g. to fade a live fitness discount — see
/// <see cref="ExpectedPointsEngine.EstimatePoints"/>).
/// </summary>
public readonly record struct FixtureDifficultyEntry(int Difficulty, int OpponentTeamId, bool IsHome, int EventId);

public static class FixtureDifficultyCalculator
{
    /// <summary>Average fixture difficulty per team over the next N unplayed gameweeks.</summary>
    public static Dictionary<int, double> AverageUpcomingDifficultyByTeam(IReadOnlyList<Fixture> fixtures, int lookaheadWeeks)
        => RawUpcomingDifficultiesByTeam(fixtures, lookaheadWeeks).ToDictionary(kv => kv.Key, kv => kv.Value.Average(e => e.Difficulty));

    /// <summary>
    /// Each team's per-fixture difficulty over the next N unplayed gameweeks, left unaveraged so
    /// callers can sum a per-fixture contribution instead — naturally yielding nothing for a blank
    /// gameweek and counting twice for a double.
    /// </summary>
    public static Dictionary<int, List<FixtureDifficultyEntry>> RawUpcomingDifficultiesByTeam(IReadOnlyList<Fixture> fixtures, int lookaheadWeeks)
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
    public static Dictionary<int, List<FixtureDifficultyEntry>> RawDifficultiesForEvent(IReadOnlyList<Fixture> fixtures, int eventId)
        => BuildRawDifficulties(fixtures.Where(f => f.Event == eventId));

    private static Dictionary<int, List<FixtureDifficultyEntry>> BuildRawDifficulties(IEnumerable<Fixture> fixtures)
    {
        var difficultiesByTeam = new Dictionary<int, List<FixtureDifficultyEntry>>();

        void AddDifficulty(int teamId, int difficulty, int opponentTeamId, bool isHome, int eventId)
        {
            if (!difficultiesByTeam.TryGetValue(teamId, out var list))
            {
                list = [];
                difficultiesByTeam[teamId] = list;
            }
            list.Add(new FixtureDifficultyEntry(difficulty, opponentTeamId, isHome, eventId));
        }

        foreach (var fixture in fixtures)
        {
            var eventId = fixture.Event ?? 0;
            AddDifficulty(fixture.TeamH, fixture.TeamHDifficulty, fixture.TeamA, isHome: true, eventId);
            AddDifficulty(fixture.TeamA, fixture.TeamADifficulty, fixture.TeamH, isHome: false, eventId);
        }

        return difficultiesByTeam;
    }
}
