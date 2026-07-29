using FplApp.Core.Models;

namespace FplApp.Api.Services;

public interface IFplDataService
{
    /// <summary>Gets the bootstrap-static payload (players, teams, gameweeks), served from cache when available.</summary>
    Task<BootstrapStatic> GetBootstrapStaticAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets all fixtures. Not cached, since scores and kickoff state change during play.</summary>
    Task<List<Fixture>> GetFixturesAsync(CancellationToken cancellationToken = default);
}
