namespace FplApp.Api.Services.LiveFpl;

public interface ILiveFplService
{
    /// <summary>
    /// How closely a manager's current starting XI + captain matches other managers this
    /// gameweek, as a percentage (0-100). Null if livefpl.net didn't have data for this team
    /// or the lookup failed.
    /// </summary>
    Task<double?> GetCloneRatingPercentAsync(int teamId, CancellationToken cancellationToken = default);
}
