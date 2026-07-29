using FplApp.Api.Services;

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

builder.Services.AddScoped<IFplDataService, FplDataService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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

app.Run();
