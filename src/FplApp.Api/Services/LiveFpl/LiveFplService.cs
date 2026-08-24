using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace FplApp.Api.Services.LiveFpl;

/// <summary>
/// Reads the "clones" stat from livefpl.net's unofficial rank-tracking API. This isn't a
/// published/documented API, so every call is defensive: any failure just means no clone
/// rating for that manager rather than breaking the rest of the page.
/// </summary>
public class LiveFplService : ILiveFplService
{
    public const string HttpClientName = "LiveFpl";

    // A manager's starting XI is 11 players; livefpl's avg_similarity is the average number of
    // those 11 that other managers also have in their starting XI this gameweek.
    private const int StartingXiSize = 11;

    private static readonly TimeSpan CloneRatingCacheDuration = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LiveFplService> _logger;

    public LiveFplService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<LiveFplService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<double?> GetCloneRatingPercentAsync(int teamId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync<double?>($"livefpl:clone:{teamId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CloneRatingCacheDuration;

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.GetAsync($"livefplapi/{teamId}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!doc.RootElement.TryGetProperty("avg_similarity", out var avgSimilarityElement) ||
                    !avgSimilarityElement.TryGetDouble(out var avgSimilarity))
                {
                    return null;
                }

                return avgSimilarity / StartingXiSize * 100;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch livefpl clone rating for team {TeamId}", teamId);
                return null;
            }
        });
    }
}
