using FplApp.Api.Models;

namespace FplApp.Api.Services.FotMob;

public interface IFotMobLineupService
{
    /// <summary>Gets predicted/confirmed lineups for every FPL fixture in the given gameweek.</summary>
    Task<List<PredictedLineup>> GetLineupsForEventAsync(int eventId, CancellationToken cancellationToken = default);
}
