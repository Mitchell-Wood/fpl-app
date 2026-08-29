using FplApp.Api.Services;
using FplApp.Api.Services.FotMob;
using FplApp.Api.Services.LiveFpl;
using FplApp.Core.Models;
using FplApp.Core.Recommendations;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient(FplDataService.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://fantasy.premierleague.com/api/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (compatible; FplApp/1.0; +https://github.com/)");
});

builder.Services.AddHttpClient(FotMobLineupService.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://www.fotmob.com/api/data/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.DefaultRequestHeaders.Referrer = new Uri("https://www.fotmob.com/");
});

builder.Services.AddHttpClient(LiveFplService.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://www.livefpl.net/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (compatible; FplApp/1.0; +https://github.com/)");
});

builder.Services.AddScoped<IFplDataService, FplDataService>();
builder.Services.AddScoped<IFotMobLineupService, FotMobLineupService>();
builder.Services.AddScoped<ILiveFplService, LiveFplService>();
builder.Services.AddSingleton<PlayerRecommendationService>();
builder.Services.AddSingleton<SquadAnalysisService>();
builder.Services.AddSingleton<CaptaincyService>();
builder.Services.AddSingleton<PriceChangeWatchService>();
builder.Services.AddSingleton<TransferPlannerService>();
builder.Services.AddSingleton<SquadBuilderService>();
builder.Services.AddSingleton<LineupOptimizerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
// In production this runs behind a proxy (e.g. Render) that terminates TLS
// and forwards plain HTTP, so HTTPS redirection is skipped to avoid a loop.

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/bootstrap-static", async (IFplDataService fplDataService, CancellationToken cancellationToken) =>
    {
        var data = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
        return Results.Ok(data);
    })
    .WithName("GetBootstrapStatic");

app.MapGet("/api/fixtures", async (IFplDataService fplDataService, CancellationToken cancellationToken) =>
    {
        var data = await fplDataService.GetFixturesAsync(cancellationToken);
        return Results.Ok(data);
    })
    .WithName("GetFixtures");

app.MapGet("/api/predicted-lineups", async (int eventId, IFotMobLineupService lineupService, CancellationToken cancellationToken) =>
    {
        var data = await lineupService.GetLineupsForEventAsync(eventId, cancellationToken);
        return Results.Ok(data);
    })
    .WithName("GetPredictedLineups");

app.MapGet("/api/player-recommendations", async (int? elementType, int? count, int? fixtureWeeks, IFplDataService fplDataService, PlayerRecommendationService recommendationService, CancellationToken cancellationToken) =>
    {
        var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
        var fixtures = await fplDataService.GetFixturesAsync(cancellationToken);
        var recommendations = recommendationService.RecommendPlayers(bootstrap, fixtures, elementType, count ?? 10, fixtureWeeks ?? 5);
        return Results.Ok(recommendations);
    })
    .WithName("GetPlayerRecommendations");

app.MapGet("/api/my-team", async (int teamId, int eventId, IFplDataService fplDataService, SquadAnalysisService squadAnalysisService, LineupOptimizerService lineupOptimizerService, CancellationToken cancellationToken) =>
    {
        var entry = await fplDataService.GetEntryAsync(teamId, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound(new { message = "No FPL team found with that ID." });
        }

        var managerName = $"{entry.PlayerFirstName} {entry.PlayerLastName}".Trim();
        var picks = await fplDataService.GetPicksAsync(teamId, eventId, cancellationToken);
        if (picks is null)
        {
            return Results.Ok(new
            {
                teamId,
                teamName = entry.Name,
                managerName,
                eventId,
                available = false,
            });
        }

        var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
        var fixtures = await fplDataService.GetFixturesAsync(cancellationToken);
        var history = await fplDataService.GetHistoryAsync(teamId, cancellationToken);
        var squad = squadAnalysisService.AnalyzeSquad(bootstrap, fixtures, picks);

        // Recommend a starting XI/captain for the next gameweek (the one whose deadline hasn't
        // passed yet) rather than describing the already-locked lineup for eventId — by the time
        // picks are public, it's too late to change that gameweek's team anyway.
        var targetEventId = bootstrap.Events.FirstOrDefault(e => e.IsNext)?.Id ?? eventId;
        var recommendation = lineupOptimizerService.OptimizeLineup(bootstrap, fixtures, picks, targetEventId);
        var playersById = bootstrap.Elements.ToDictionary(p => p.Id);
        var teamsById = bootstrap.Teams.ToDictionary(t => t.Id);
        var targetEventFixtures = fixtures.Where(f => f.Event == targetEventId).ToList();
        foreach (var pick in squad)
        {
            if (recommendation.ByPlayerId.TryGetValue(pick.PlayerId, out var rec))
            {
                pick.IsBenched = !rec.IsStarting;
                pick.IsCaptain = rec.IsCaptain;
                pick.IsViceCaptain = rec.IsViceCaptain;
                pick.ExpectedPointsNextGameweek = rec.ExpectedPoints;
            }
            if (playersById.TryGetValue(pick.PlayerId, out var player))
            {
                pick.NextFixtures = BuildNextFixtures(player, teamsById, targetEventFixtures);
            }
        }

        return Results.Ok(new
        {
            teamId,
            teamName = entry.Name,
            managerName,
            eventId,
            targetEventId,
            formation = recommendation.Formation,
            available = true,
            bank = picks.EntryHistory.Bank / 10.0,
            value = picks.EntryHistory.Value / 10.0,
            picks = squad,
            chips = BuildChipStatus(picks.ActiveChip, history?.Chips, eventId),
        });
    })
    .WithName("GetMyTeam");

app.MapGet("/api/captain-suggestions", async (int teamId, int eventId, IFplDataService fplDataService, CaptaincyService captaincyService, LineupOptimizerService lineupOptimizerService, CancellationToken cancellationToken) =>
    {
        var entry = await fplDataService.GetEntryAsync(teamId, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound(new { message = "No FPL team found with that ID." });
        }

        var picks = await fplDataService.GetPicksAsync(teamId, eventId, cancellationToken);
        if (picks is null)
        {
            return Results.Ok(new { teamId, eventId, available = false });
        }

        var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
        var fixtures = await fplDataService.GetFixturesAsync(cancellationToken);

        // Captaincy only matters for a gameweek whose deadline hasn't passed yet, so score against
        // the next upcoming gameweek's fixtures rather than the (already-locked) squad's own one —
        // otherwise "suggestions" would just describe a captain choice it's too late to change. Rank
        // the recommended starting XI (the one shown on the pitch), not whichever 11 were actually
        // declared for the locked gameweek, so the two stay consistent.
        var targetEventId = bootstrap.Events.FirstOrDefault(e => e.IsNext)?.Id ?? eventId;
        var recommendation = lineupOptimizerService.OptimizeLineup(bootstrap, fixtures, picks, targetEventId);
        var recommendedPicks = new TeamPicks
        {
            ActiveChip = picks.ActiveChip,
            EntryHistory = picks.EntryHistory,
            Picks = picks.Picks.Select(p =>
            {
                var rec = recommendation.ByPlayerId.GetValueOrDefault(p.Element);
                return new Pick
                {
                    Element = p.Element,
                    Position = rec?.IsStarting == true ? 1 : 12,
                    Multiplier = p.Multiplier,
                    IsCaptain = rec?.IsCaptain ?? false,
                    IsViceCaptain = rec?.IsViceCaptain ?? false,
                };
            }).ToList(),
        };
        var suggestions = captaincyService.SuggestCaptains(bootstrap, fixtures, recommendedPicks, targetEventId);

        return Results.Ok(new { teamId, eventId, available = true, suggestions });
    })
    .WithName("GetCaptainSuggestions");

app.MapGet("/api/price-watch", async (int? count, IFplDataService fplDataService, PriceChangeWatchService priceChangeWatchService, CancellationToken cancellationToken) =>
    {
        var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
        var result = priceChangeWatchService.GetPriceWatch(bootstrap, count ?? 15);
        return Results.Ok(result);
    })
    .WithName("GetPriceWatch");

app.MapGet("/api/transfer-suggestions", async (int teamId, int eventId, int? fixtureWeeks, int? perPlayer, int? freeTransfers, IFplDataService fplDataService, SquadAnalysisService squadAnalysisService, TransferPlannerService transferPlannerService, CancellationToken cancellationToken) =>
    {
        var entry = await fplDataService.GetEntryAsync(teamId, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound(new { message = "No FPL team found with that ID." });
        }

        var picks = await fplDataService.GetPicksAsync(teamId, eventId, cancellationToken);
        if (picks is null)
        {
            return Results.Ok(new { teamId, eventId, available = false });
        }

        var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
        var fixtures = await fplDataService.GetFixturesAsync(cancellationToken);
        var squad = squadAnalysisService.AnalyzeSquad(bootstrap, fixtures, picks);
        var suggestions = transferPlannerService.SuggestTransfers(
            bootstrap, fixtures, squad, picks.EntryHistory.Bank, fixtureWeeks ?? 5, perPlayer ?? 3);
        var fundedUpgrade = transferPlannerService.SuggestFundedUpgrade(
            bootstrap, fixtures, squad, picks.EntryHistory.Bank, fixtureWeeks ?? 5);
        var plan = transferPlannerService.BuildTransferPlan(
            bootstrap, fixtures, squad, picks.EntryHistory.Bank, fixtureWeeks ?? 5, freeTransfers ?? 1);

        return Results.Ok(new { teamId, eventId, available = true, suggestions, fundedUpgrade, plan });
    })
    .WithName("GetTransferSuggestions");

app.MapGet("/api/my-leagues", async (int teamId, IFplDataService fplDataService, CancellationToken cancellationToken) =>
    {
        var entry = await fplDataService.GetEntryAsync(teamId, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound(new { message = "No FPL team found with that ID." });
        }

        var managerName = $"{entry.PlayerFirstName} {entry.PlayerLastName}".Trim();
        return Results.Ok(new
        {
            teamId,
            teamName = entry.Name,
            managerName,
            leagues = entry.Leagues.Classic.Select(l => new { l.Id, l.Name }),
        });
    })
    .WithName("GetMyLeagues");

const int FixturesLeftMaxLeagueSize = 20;

app.MapGet("/api/league-standings", async (int leagueId, int? page, IFplDataService fplDataService, ILiveFplService liveFplService, CancellationToken cancellationToken) =>
    {
        var standings = await fplDataService.GetLeagueStandingsAsync(leagueId, page ?? 1, cancellationToken);
        if (standings is null)
        {
            return Results.NotFound(new { message = "No league found with that ID." });
        }

        var rows = standings.Standings.Results;

        // Fixtures-remaining, chip status and clone rating each need one extra API call per
        // manager (there's no bulk picks/history endpoint, and livefpl.net is per-manager too),
        // so it's only worth the round-trips for small leagues you can eyeball at a glance.
        Dictionary<int, int>? fixturesLeftByEntry = null;
        Dictionary<int, bool>? captainYetToPlayByEntry = null;
        Dictionary<int, Dictionary<string, string>>? chipStatusByEntry = null;
        Dictionary<int, int>? estimatedFreeTransfersByEntry = null;
        Dictionary<int, double>? cloneRatingByEntry = null;
        Dictionary<int, double>? templateRatingByEntry = null;
        if (rows.Count > 0 && rows.Count <= FixturesLeftMaxLeagueSize)
        {
            var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
            var currentEventId = bootstrap.Events.FirstOrDefault(e => e.IsCurrent)?.Id;
            if (currentEventId is { } eventId)
            {
                var fixtures = await fplDataService.GetFixturesAsync(cancellationToken);
                var eventLive = await fplDataService.GetEventLiveAsync(eventId, cancellationToken);
                var minutesByElementId = eventLive.Elements.ToDictionary(e => e.Id, e => e.Stats.Minutes);
                var perEntryData = await Task.WhenAll(rows.Select(async r =>
                {
                    var picksTask = fplDataService.GetPicksAsync(r.Entry, eventId, cancellationToken);
                    var historyTask = fplDataService.GetHistoryAsync(r.Entry, cancellationToken);
                    var liveFplStatsTask = liveFplService.GetStatsAsync(r.Entry, cancellationToken);
                    await Task.WhenAll(picksTask, historyTask, liveFplStatsTask);
                    return (r.Entry, Picks: picksTask.Result, History: historyTask.Result, LiveFplStats: liveFplStatsTask.Result);
                }));

                fixturesLeftByEntry = perEntryData
                    .Where(p => p.Picks is not null)
                    .ToDictionary(p => p.Entry, p => FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, p.Picks!, eventId, minutesByElementId));

                captainYetToPlayByEntry = perEntryData
                    .Where(p => p.Picks is not null)
                    .Select(p => (p.Entry, YetToPlay: FixturesRemainingCalculator.CaptainHasFixtureRemaining(bootstrap, fixtures, p.Picks!, eventId)))
                    .Where(p => p.YetToPlay.HasValue)
                    .ToDictionary(p => p.Entry, p => p.YetToPlay!.Value);

                chipStatusByEntry = perEntryData.ToDictionary(p => p.Entry, p => BuildChipStatus(p.Picks?.ActiveChip, p.History?.Chips, eventId));

                estimatedFreeTransfersByEntry = perEntryData
                    .Where(p => p.History is not null)
                    .ToDictionary(p => p.Entry, p => FreeTransferEstimator.EstimateAvailable(p.History!.Current, p.History!.Chips));

                cloneRatingByEntry = perEntryData
                    .Where(p => p.LiveFplStats.CloneRatingPercent is not null)
                    .ToDictionary(p => p.Entry, p => p.LiveFplStats.CloneRatingPercent!.Value);

                templateRatingByEntry = perEntryData
                    .Where(p => p.LiveFplStats.TemplateRatingPercent is not null)
                    .ToDictionary(p => p.Entry, p => p.LiveFplStats.TemplateRatingPercent!.Value);
            }
        }

        return Results.Ok(new
        {
            leagueId,
            leagueName = standings.League.Name,
            hasManagerDetail = fixturesLeftByEntry != null,
            hasNext = standings.Standings.HasNext,
            page = standings.Standings.Page,
            results = rows.Select(r => new
            {
                entry = r.Entry,
                entryName = r.EntryName,
                playerName = r.PlayerName,
                rank = r.Rank,
                lastRank = r.LastRank,
                total = r.Total,
                eventTotal = r.EventTotal,
                fixturesLeft = fixturesLeftByEntry != null && fixturesLeftByEntry.TryGetValue(r.Entry, out var left) ? (int?)left : null,
                captainYetToPlay = captainYetToPlayByEntry != null && captainYetToPlayByEntry.TryGetValue(r.Entry, out var captainYetToPlay) && captainYetToPlay,
                estimatedFreeTransfers = estimatedFreeTransfersByEntry != null && estimatedFreeTransfersByEntry.TryGetValue(r.Entry, out var freeTransfers) ? (int?)freeTransfers : null,
                chips = chipStatusByEntry != null && chipStatusByEntry.TryGetValue(r.Entry, out var chipStatus) ? chipStatus : null,
                cloneRatingPercent = cloneRatingByEntry != null && cloneRatingByEntry.TryGetValue(r.Entry, out var cloneRating) ? (double?)cloneRating : null,
                templateRatingPercent = templateRatingByEntry != null && templateRatingByEntry.TryGetValue(r.Entry, out var templateRating) ? (double?)templateRating : null,
            }),
        });
    })
    .WithName("GetLeagueStandings");

static List<CaptainFixture> BuildNextFixtures(Player player, Dictionary<int, Team> teamsById, List<Fixture> eventFixtures)
{
    var result = new List<CaptainFixture>();
    foreach (var fixture in eventFixtures.Where(f => f.TeamH == player.Team || f.TeamA == player.Team))
    {
        var isHome = fixture.TeamH == player.Team;
        var opponentId = isHome ? fixture.TeamA : fixture.TeamH;
        var difficulty = isHome ? fixture.TeamHDifficulty : fixture.TeamADifficulty;
        result.Add(new CaptainFixture
        {
            Opponent = teamsById.GetValueOrDefault(opponentId)?.ShortName ?? "?",
            Venue = isHome ? "H" : "A",
            Difficulty = difficulty,
        });
    }
    return result;
}

// Each gameweek 1-19 and 20-38 half of the season grants its own fresh set of all four chips —
// an unused chip from the first half doesn't carry over, and a chip already played in the first
// half becomes available again in the second.
const int SecondHalfStartEvent = 20;

static bool SameChipHalf(int eventA, int eventB)
    => (eventA < SecondHalfStartEvent) == (eventB < SecondHalfStartEvent);

// Maps each of the four chips to "active" (being played this gameweek), "used" (already played
// earlier in the same half of the season), or "available" (not yet played this half).
static Dictionary<string, string> BuildChipStatus(string? activeChip, IReadOnlyList<ChipPlay>? chipHistory, int eventId)
{
    var usedElsewhere = (chipHistory ?? [])
        .Where(c => c.Event != eventId && SameChipHalf(c.Event, eventId))
        .Select(c => c.Name)
        .ToHashSet();

    string StatusFor(string chipName)
    {
        if (activeChip == chipName)
        {
            return "active";
        }
        return usedElsewhere.Contains(chipName) ? "used" : "available";
    }

    return new Dictionary<string, string>
    {
        ["tc"] = StatusFor("3xc"),
        ["bb"] = StatusFor("bboost"),
        ["fh"] = StatusFor("freehit"),
        ["wc"] = StatusFor("wildcard"),
    };
}

app.MapGet("/api/manager-history", async (int teamId, IFplDataService fplDataService, CancellationToken cancellationToken) =>
    {
        var entry = await fplDataService.GetEntryAsync(teamId, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound(new { message = "No FPL team found with that ID." });
        }

        var history = await fplDataService.GetHistoryAsync(teamId, cancellationToken);
        if (history is null)
        {
            return Results.NotFound(new { message = "No history found for that team ID." });
        }

        var managerName = $"{entry.PlayerFirstName} {entry.PlayerLastName}".Trim();
        var estimatedFreeTransfers = FreeTransferEstimator.EstimateAvailable(history.Current, history.Chips);

        return Results.Ok(new
        {
            teamId,
            teamName = entry.Name,
            managerName,
            estimatedFreeTransfers,
            gameweeks = history.Current.Select(gw => new
            {
                @event = gw.Event,
                points = gw.Points,
                totalPoints = gw.TotalPoints,
                bank = gw.Bank,
                value = gw.Value,
                eventTransfers = gw.EventTransfers,
                eventTransfersCost = gw.EventTransfersCost,
                pointsOnBench = gw.PointsOnBench,
            }),
            chips = history.Chips.Select(c => new { name = c.Name, @event = c.Event }),
        });
    })
    .WithName("GetManagerHistory");

app.MapGet("/api/manager-transfers", async (int teamId, IFplDataService fplDataService, CancellationToken cancellationToken) =>
    {
        var transfers = await fplDataService.GetTransfersAsync(teamId, cancellationToken);
        return Results.Ok(transfers
            .OrderByDescending(t => t.Time)
            .Select(t => new
            {
                @event = t.Event,
                elementIn = t.ElementIn,
                elementInCost = t.ElementInCost,
                elementOut = t.ElementOut,
                elementOutCost = t.ElementOutCost,
                time = t.Time,
            }));
    })
    .WithName("GetManagerTransfers");

app.MapGet("/api/wildcard-squad", async (int? teamId, int? eventId, int? budget, int? fixtureLookaheadWeeks, IFplDataService fplDataService, SquadBuilderService squadBuilderService, CancellationToken cancellationToken) =>
    {
        var lookahead = fixtureLookaheadWeeks ?? 1;
        int resolvedBudget;

        if (budget is { } explicitBudget)
        {
            // A manual override always wins, even with a team loaded — e.g. "what if I had less to spend".
            resolvedBudget = explicitBudget;
        }
        else if (teamId is { } id && eventId is { } evt)
        {
            var entry = await fplDataService.GetEntryAsync(id, cancellationToken);
            if (entry is null)
            {
                return Results.NotFound(new { message = "No FPL team found with that ID." });
            }

            var picks = await fplDataService.GetPicksAsync(id, evt, cancellationToken);
            if (picks is null)
            {
                return Results.Ok(new { teamId = id, eventId = evt, available = false });
            }

            // Bank plus current squad value: what a full rebuild (Wildcard/Free Hit) has to spend.
            resolvedBudget = picks.EntryHistory.Bank + picks.EntryHistory.Value;
        }
        else
        {
            resolvedBudget = 1000; // £100.0m — the standard starting budget, used when no team is loaded
        }

        var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
        var fixtures = await fplDataService.GetFixturesAsync(cancellationToken);

        try
        {
            var squad = squadBuilderService.BuildSquad(bootstrap, fixtures, resolvedBudget, lookahead);
            return Results.Ok(new { available = true, budget = resolvedBudget, fixtureLookaheadWeeks = lookahead, squad });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("GetWildcardSquad");

app.Run();
