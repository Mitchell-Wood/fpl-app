using FplApp.Api.Models;
using FplApp.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FplApp.Api.Services.FotMob;

public class FotMobLineupService : IFotMobLineupService
{
    public const string HttpClientName = "FotMob";

    // FotMob's league id for the Premier League.
    private const int PremierLeagueId = 47;

    private const string FixturesCacheKey = "fotmob:pl-fixtures";
    private static readonly TimeSpan FixturesCacheDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan MatchDetailsCacheDuration = TimeSpan.FromMinutes(10);

    // Maps FPL's team short_name to FotMob's team id for the current Premier League season.
    // Update this when clubs are promoted/relegated (usually 3 entries per season).
    private static readonly Dictionary<string, int> FplShortNameToFotMobTeamId = new()
    {
        ["ARS"] = 9825,
        ["AVL"] = 10252,
        ["BOU"] = 8678,
        ["BRE"] = 9937,
        ["BHA"] = 10204,
        ["CHE"] = 8455,
        ["COV"] = 8669,
        ["CRY"] = 9826,
        ["EVE"] = 8668,
        ["FUL"] = 9879,
        ["HUL"] = 8667,
        ["IPS"] = 9902,
        ["LEE"] = 8463,
        ["LIV"] = 8650,
        ["MCI"] = 8456,
        ["MUN"] = 10260,
        ["NEW"] = 10261,
        ["NFO"] = 10203,
        ["TOT"] = 8586,
        ["SUN"] = 8472,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFplDataService _fplDataService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FotMobLineupService> _logger;

    public FotMobLineupService(
        IHttpClientFactory httpClientFactory,
        IFplDataService fplDataService,
        IMemoryCache cache,
        ILogger<FotMobLineupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _fplDataService = fplDataService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<PredictedLineup>> GetLineupsForEventAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var bootstrap = await _fplDataService.GetBootstrapStaticAsync(cancellationToken);
        var fixtures = await _fplDataService.GetFixturesAsync(cancellationToken);
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var fotmobFixturesByTeamPair = await GetFotMobFixturesByTeamPairAsync(cancellationToken);

        var results = new List<PredictedLineup>();

        foreach (var fixture in fixtures.Where(f => f.Event == eventId))
        {
            var result = new PredictedLineup { FixtureId = fixture.Id, KickoffTime = fixture.KickoffTime };
            results.Add(result);

            try
            {
                if (!teamsById.TryGetValue(fixture.TeamH, out var homeTeam) ||
                    !teamsById.TryGetValue(fixture.TeamA, out var awayTeam))
                {
                    continue;
                }

                if (!FplShortNameToFotMobTeamId.TryGetValue(homeTeam.ShortName, out var fotmobHomeId) ||
                    !FplShortNameToFotMobTeamId.TryGetValue(awayTeam.ShortName, out var fotmobAwayId))
                {
                    continue;
                }

                var fotmobHomeIdStr = fotmobHomeId.ToString();
                var fotmobAwayIdStr = fotmobAwayId.ToString();
                if (!fotmobFixturesByTeamPair.TryGetValue((fotmobHomeIdStr, fotmobAwayIdStr), out var fotmobMatchId))
                {
                    continue;
                }

                var lineup = await GetMatchLineupAsync(fotmobMatchId, cancellationToken);
                if (lineup is null)
                {
                    continue;
                }

                var homeHasStarters = lineup.HomeTeam.Starters.Count > 0;
                var awayHasStarters = lineup.AwayTeam.Starters.Count > 0;
                if (!homeHasStarters && !awayHasStarters)
                {
                    continue;
                }

                result.Status = lineup.LineupType == "standard" ? "confirmed" : "predicted";
                result.HomeTeam = MapTeamLineup(lineup.HomeTeam);
                result.AwayTeam = MapTeamLineup(lineup.AwayTeam);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch FotMob lineup for fixture {FixtureId}", fixture.Id);
            }
        }

        return results;
    }

    private async Task<FotMobLineup?> GetMatchLineupAsync(string fotmobMatchId, CancellationToken cancellationToken)
    {
        var cacheKey = $"fotmob:match:{fotmobMatchId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = MatchDetailsCacheDuration;
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetFromJsonAsync<FotMobMatchDetailsResponse>(
                $"matchDetails?matchId={fotmobMatchId}", cancellationToken);
            return response?.Content?.Lineup;
        });
    }

    private async Task<Dictionary<(string Home, string Away), string>> GetFotMobFixturesByTeamPairAsync(CancellationToken cancellationToken)
    {
        var cached = await _cache.GetOrCreateAsync(FixturesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = FixturesCacheDuration;
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetFromJsonAsync<FotMobLeagueFixturesResponse>(
                $"leagues?id={PremierLeagueId}&type=league", cancellationToken);
            var matches = response?.Fixtures?.AllMatches ?? [];
            return matches.ToDictionary(m => (m.Home.Id, m.Away.Id), m => m.Id);
        });

        return cached ?? [];
    }

    private static LineupTeam MapTeamLineup(FotMobTeamLineup team) => new()
    {
        Name = team.Name,
        Formation = team.Formation,
        Starters = team.Starters.Select(MapPlayer).ToList(),
        Subs = team.Subs.Select(MapPlayer).ToList(),
        Unavailable = team.Unavailable.Select(p => new UnavailablePlayer
        {
            Name = p.Name,
            Reason = p.Unavailability?.ExpectedReturn is { Length: > 0 } expectedReturn
                ? $"{p.Unavailability.Type} ({expectedReturn})"
                : p.Unavailability?.Type,
        }).ToList(),
    };

    private static LineupPlayer MapPlayer(FotMobPlayer player) => new()
    {
        Name = player.Name,
        ShirtNumber = player.ShirtNumber,
        IsCaptain = player.IsCaptain,
        Position = player.UsualPlayingPositionId switch
        {
            0 => "GK",
            1 => "DEF",
            2 => "MID",
            3 => "FWD",
            _ => "?",
        },
    };
}
