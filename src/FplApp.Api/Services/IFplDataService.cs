using FplApp.Core.Models;

namespace FplApp.Api.Services;

public interface IFplDataService
{
    /// <summary>Gets the bootstrap-static payload (players, teams, gameweeks), served from cache when available.</summary>
    Task<BootstrapStatic> GetBootstrapStaticAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets all fixtures. Not cached, since scores and kickoff state change during play.</summary>
    Task<List<Fixture>> GetFixturesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets basic manager/team info, or null if the team id doesn't exist.</summary>
    Task<TeamEntry?> GetEntryAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a manager's squad for a gameweek, or null if not found — which is the normal response
    /// for the current gameweek before its deadline has passed, since FPL doesn't publish picks
    /// early (so rivals can't scout your team before you're locked in).
    /// </summary>
    Task<TeamPicks?> GetPicksAsync(int teamId, int eventId, CancellationToken cancellationToken = default);

    /// <summary>Gets a classic league's standings page, or null if the league id doesn't exist.</summary>
    Task<LeagueStandingsResponse?> GetLeagueStandingsAsync(int leagueId, int page, CancellationToken cancellationToken = default);

    /// <summary>Gets a manager's season-to-date gameweek history and chips played (always public).</summary>
    Task<TeamHistoryResponse?> GetHistoryAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>Gets every player's live stats (minutes played, points, etc.) for one gameweek.</summary>
    Task<EventLiveResponse> GetEventLiveAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Gets every transfer a manager has made this season (always public), newest first.</summary>
    Task<List<TeamTransfer>> GetTransfersAsync(int teamId, CancellationToken cancellationToken = default);
}
