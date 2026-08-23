using FplApp.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FplApp.Api.Services;

public class FplDataService : IFplDataService
{
    public const string HttpClientName = "FplApi";
    private const string BootstrapStaticCacheKey = "fpl:bootstrap-static";
    private static readonly TimeSpan BootstrapStaticCacheDuration = TimeSpan.FromMinutes(15);

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
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync($"entry/{teamId}/", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TeamEntry>(cancellationToken);
    }

    public async Task<TeamPicks?> GetPicksAsync(int teamId, int eventId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync($"entry/{teamId}/event/{eventId}/picks/", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TeamPicks>(cancellationToken);
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
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync($"entry/{teamId}/history/", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TeamHistoryResponse>(cancellationToken);
    }

    public async Task<EventLiveResponse> GetEventLiveAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var result = await client.GetFromJsonAsync<EventLiveResponse>($"event/{eventId}/live/", cancellationToken);
        return result ?? new EventLiveResponse();
    }

    public async Task<List<TeamTransfer>> GetTransfersAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var result = await client.GetFromJsonAsync<List<TeamTransfer>>($"entry/{teamId}/transfers/", cancellationToken);
        return result ?? [];
    }
}
