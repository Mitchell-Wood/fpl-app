using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace FplApp.Api.Services.LiveFpl;

/// <summary>
/// Reads per-manager stats from livefpl.net's unofficial rank-tracking API. This isn't a
/// published/documented API, so every call is defensive: any failure just means no stats for
/// that manager rather than breaking the rest of the page.
/// </summary>
public class LiveFplService : ILiveFplService
{
    public const string HttpClientName = "LiveFpl";

    // A manager's starting XI is 11 players; livefpl's avg_similarity is the average number of
    // those 11 that other managers also have in their starting XI this gameweek.
    private const int StartingXiSize = 11;

    private static readonly TimeSpan StatsCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly LiveFplStats EmptyStats = new(null, null);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LiveFplService> _logger;

    public LiveFplService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<LiveFplService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<LiveFplStats> GetStatsAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var stats = await _cache.GetOrCreateAsync($"livefpl:stats:{teamId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = StatsCacheDuration;

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.GetAsync($"livefplapi/{teamId}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return EmptyStats;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                double? cloneRating = root.TryGetProperty("avg_similarity", out var avgSimilarityElement) &&
                    avgSimilarityElement.TryGetDouble(out var avgSimilarity)
                        ? avgSimilarity / StartingXiSize * 100
                        : null;

                double? templateRating = root.TryGetProperty("template", out var templateElement) &&
                    templateElement.TryGetDouble(out var template)
                        ? template
                        : null;

                return new LiveFplStats(cloneRating, templateRating);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch livefpl stats for team {TeamId}", teamId);
                return EmptyStats;
            }
        });

        return stats ?? EmptyStats;
    }
}
