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
        var suggestions = captaincyService.SuggestCaptains(bootstrap, fixtures, picks, eventId);

        return Results.Ok(new { teamId, eventId, available = true, suggestions });
    })
    .WithName("GetCaptainSuggestions");

app.Run();
