namespace FplApp.Api.Services.LiveFpl;

public record LiveFplStats(double? CloneRatingPercent, double? TemplateRatingPercent);

public interface ILiveFplService
{
    /// <summary>
    /// A manager's "clone rating" (how closely their starting XI + captain matches other
    /// managers this gameweek) and "template rating" (how heavy each line of their team is
    /// compared to top 10k teams), both as percentages (0-100). Either can be null if
    /// livefpl.net didn't have data for this team or the lookup failed.
    /// </summary>
    Task<LiveFplStats> GetStatsAsync(int teamId, CancellationToken cancellationToken = default);
}
