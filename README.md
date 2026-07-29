# FplApp

A .NET 9 solution that proxies data from the [Fantasy Premier League API](https://fantasy.premierleague.com/api/) and provides a foundation for transfer/team recommendation logic.

## Projects

- **`src/FplApp.Core`** — class library with domain models (`BootstrapStatic`, `Player`, `Team`, `Event`, `ElementType`, `Fixture`) and a `PlayerRecommendationService` that ranks available players by recent form and points-per-cost.
- **`src/FplApp.Api`** — ASP.NET Core Web API that fetches and serves FPL data. `FplDataService` calls the upstream API via `IHttpClientFactory` and caches the `bootstrap-static` response in memory for 15 minutes (it changes infrequently); fixtures are always fetched fresh since scores/state change during live play.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Setup

```bash
git clone https://github.com/Mitchell-Wood/fpl-app.git
cd fpl-app
dotnet restore
```

## Running the API

```bash
dotnet run --project src/FplApp.Api
```

By default this listens on `http://localhost:5140` (see `src/FplApp.Api/Properties/launchSettings.json` for the HTTPS profile). Once running, try:

```bash
curl http://localhost:5140/api/bootstrap-static
curl http://localhost:5140/api/fixtures
```

Or use the requests in `src/FplApp.Api/FplApp.Api.http` (Visual Studio / VS Code REST Client / Rider).

In `Development` environment, an OpenAPI document is available at `/openapi/v1.json`.

## Endpoints

| Endpoint | Description | Caching |
|---|---|---|
| `GET /api/bootstrap-static` | Players, teams, gameweeks (proxies `bootstrap-static/`) | In-memory, 15 minutes |
| `GET /api/fixtures` | All fixtures for the season (proxies `fixtures/`) | None — fetched live |

## Building and testing

```bash
dotnet build FplApp.sln
```

There are no automated tests yet.

## Notes

- The upstream FPL API requires no authentication but does expect a browser-like `User-Agent` header; this is set on the named `FplApi` `HttpClient` in `Program.cs`.
- `FplApp.Core` has no dependency on ASP.NET Core, so recommendation/domain logic can be reused from other hosts (console app, background worker, tests) without pulling in the web stack.
