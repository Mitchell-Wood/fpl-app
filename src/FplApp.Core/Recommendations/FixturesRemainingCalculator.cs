using FplApp.Core.Models;

namespace FplApp.Core.Recommendations;

public static class FixturesRemainingCalculator
{
    /// <summary>
    /// Counts how many fixture-legs a manager's starting XI (position 1-11) still has left to play
    /// in the given gameweek. A player whose team has two fixtures in the gameweek (a double
    /// gameweek) counts twice; a player whose team doesn't play at all counts zero.
    /// </summary>
    public static int CountRemaining(BootstrapStatic bootstrap, IReadOnlyList<Fixture> fixtures, TeamPicks picks, int eventId)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(picks);

        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);

        // FPL leaves "finished" false for a while after full-time, pending official confirmation
        // (bonus points etc.) — "finished_provisional" flips true immediately at the final whistle,
        // so it's the accurate signal for whether a fixture is actually still to be played.
        var eventFixtures = fixtures.Where(f => f.Event == eventId && !f.FinishedProvisional).ToList();

        var count = 0;
        foreach (var pick in picks.Picks.Where(p => p.Position <= 11))
        {
            if (!playersById.TryGetValue(pick.Element, out var player))
            {
                continue;
            }

            count += eventFixtures.Count(f => f.TeamH == player.Team || f.TeamA == player.Team);
        }

        return count;
    }
}
