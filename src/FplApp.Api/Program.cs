using FplApp.Api.Services;
using FplApp.Api.Services.FotMob;
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

builder.Services.AddScoped<IFplDataService, FplDataService>();
builder.Services.AddScoped<IFotMobLineupService, FotMobLineupService>();
builder.Services.AddSingleton<PlayerRecommendationService>();
builder.Services.AddSingleton<SquadAnalysisService>();
builder.Services.AddSingleton<CaptaincyService>();
builder.Services.AddSingleton<PriceChangeWatchService>();
builder.Services.AddSingleton<TransferPlannerService>();
builder.Services.AddSingleton<SquadBuilderService>();
builder.Services.AddSingleton<BenchBoostPlannerService>();

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

app.MapGet("/api/my-team", async (int teamId, int eventId, IFplDataService fplDataService, SquadAnalysisService squadAnalysisService, CancellationToken cancellationToken) =>
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
        var squad = squadAnalysisService.AnalyzeSquad(bootstrap, fixtures, picks);

        return Results.Ok(new
        {
            teamId,
            teamName = entry.Name,
            managerName,
            eventId,
            available = true,
            bank = picks.EntryHistory.Bank / 10.0,
            value = picks.EntryHistory.Value / 10.0,
            points = picks.EntryHistory.Points,
            picks = squad,
        });
    })
    .WithName("GetMyTeam");

app.MapGet("/api/captain-suggestions", async (int teamId, int eventId, IFplDataService fplDataService, CaptaincyService captaincyService, CancellationToken cancellationToken) =>
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
        // otherwise "suggestions" would just describe a captain choice it's too late to change.
        var targetEventId = bootstrap.Events.FirstOrDefault(e => e.IsNext)?.Id ?? eventId;
        var suggestions = captaincyService.SuggestCaptains(bootstrap, fixtures, picks, targetEventId);

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

app.MapGet("/api/league-standings", async (int leagueId, int? page, IFplDataService fplDataService, CancellationToken cancellationToken) =>
    {
        var standings = await fplDataService.GetLeagueStandingsAsync(leagueId, page ?? 1, cancellationToken);
        if (standings is null)
        {
            return Results.NotFound(new { message = "No league found with that ID." });
        }

        var rows = standings.Standings.Results;

        // Fixtures-remaining needs one extra FPL API call per manager (there's no bulk picks
        // endpoint), so it's only worth the round-trips for small leagues you can eyeball at a glance.
        Dictionary<int, int>? fixturesLeftByEntry = null;
        if (rows.Count > 0 && rows.Count <= FixturesLeftMaxLeagueSize)
        {
            var bootstrap = await fplDataService.GetBootstrapStaticAsync(cancellationToken);
            var currentEventId = bootstrap.Events.FirstOrDefault(e => e.IsCurrent)?.Id;
            if (currentEventId is { } eventId)
            {
                var fixtures = await fplDataService.GetFixturesAsync(cancellationToken);
                var eventLive = await fplDataService.GetEventLiveAsync(eventId, cancellationToken);
                var minutesByElementId = eventLive.Elements.ToDictionary(e => e.Id, e => e.Stats.Minutes);
                var picksByEntry = await Task.WhenAll(
                    rows.Select(async r => (r.Entry, Picks: await fplDataService.GetPicksAsync(r.Entry, eventId, cancellationToken))));

                fixturesLeftByEntry = picksByEntry
                    .Where(p => p.Picks is not null)
                    .ToDictionary(p => p.Entry, p => FixturesRemainingCalculator.CountRemaining(bootstrap, fixtures, p.Picks!, eventId, minutesByElementId));
            }
        }

        return Results.Ok(new
        {
            leagueId,
            leagueName = standings.League.Name,
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
            }),
        });
    })
    .WithName("GetLeagueStandings");

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

app.MapGet("/api/bench-boost-plan", async (int teamId, int eventId, int targetEventId, int? freeTransfers, IFplDataService fplDataService, SquadAnalysisService squadAnalysisService, BenchBoostPlannerService benchBoostPlannerService, CancellationToken cancellationToken) =>
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
        var plan = benchBoostPlannerService.PlanBenchBoost(
            bootstrap, fixtures, squad, picks.EntryHistory.Bank, targetEventId, freeTransfers ?? 1);

        return Results.Ok(new { teamId, eventId, available = true, plan });
    })
    .WithName("GetBenchBoostPlan");

app.Run();
