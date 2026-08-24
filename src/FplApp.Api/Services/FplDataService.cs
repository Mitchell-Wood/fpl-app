using FplApp.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FplApp.Api.Services;

public class FplDataService : IFplDataService
{
    public const string HttpClientName = "FplApi";
    private const string BootstrapStaticCacheKey = "fpl:bootstrap-static";
    private static readonly TimeSpan BootstrapStaticCacheDuration = TimeSpan.FromMinutes(15);

    // Rarely changes mid-gameweek, so cache aggressively.
    private static readonly TimeSpan EntryCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HistoryCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TransfersCacheDuration = TimeSpan.FromMinutes(10);

    // Moves during live matches, so keep a short leash.
    private static readonly TimeSpan PicksCacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EventLiveCacheDuration = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FplDataService> _logger;

    public FplDataService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<FplDataService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<BootstrapStatic> GetBootstrapStaticAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetOrCreateAsync(BootstrapStaticCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = BootstrapStaticCacheDuration;
            _logger.LogInformation("Fetching bootstrap-static from the FPL API (cache miss)");

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var result = await client.GetFromJsonAsync<BootstrapStatic>("bootstrap-static/", cancellationToken);
            return result ?? throw new InvalidOperationException("FPL bootstrap-static response was empty.");
        });

        return cached ?? throw new InvalidOperationException("FPL bootstrap-static response was empty.");
    }

    public async Task<List<Fixture>> GetFixturesAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var result = await client.GetFromJsonAsync<List<Fixture>>("fixtures/", cancellationToken);
        return result ?? [];
    }

    public async Task<TeamEntry?> GetEntryAsync(int teamId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync($"fpl:entry:{teamId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = EntryCacheDuration;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync($"entry/{teamId}/", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TeamEntry>(cancellationToken);
        });
    }

    public async Task<TeamPicks?> GetPicksAsync(int teamId, int eventId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync($"fpl:picks:{teamId}:{eventId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = PicksCacheDuration;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync($"entry/{teamId}/event/{eventId}/picks/", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TeamPicks>(cancellationToken);
        });
    }

    public async Task<LeagueStandingsResponse?> GetLeagueStandingsAsync(int leagueId, int page, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync($"leagues-classic/{leagueId}/standings/?page_standings={page}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LeagueStandingsResponse>(cancellationToken);
    }

    public async Task<TeamHistoryResponse?> GetHistoryAsync(int teamId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync($"fpl:history:{teamId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = HistoryCacheDuration;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync($"entry/{teamId}/history/", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TeamHistoryResponse>(cancellationToken);
        });
    }

    public async Task<EventLiveResponse> GetEventLiveAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetOrCreateAsync($"fpl:event-live:{eventId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = EventLiveCacheDuration;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            return await client.GetFromJsonAsync<EventLiveResponse>($"event/{eventId}/live/", cancellationToken);
        });
        return cached ?? new EventLiveResponse();
    }

    public async Task<List<TeamTransfer>> GetTransfersAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetOrCreateAsync($"fpl:transfers:{teamId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TransfersCacheDuration;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            return await client.GetFromJsonAsync<List<TeamTransfer>>($"entry/{teamId}/transfers/", cancellationToken);
        });
        return cached ?? [];
    }
}
