using System.Text.Json.Serialization;

namespace FplApp.Api.Services.FotMob;

// Shapes for the undocumented FotMob JSON endpoints (www.fotmob.com/api/data/...).
// These mirror only the fields this app actually uses.

public class FotMobLeagueFixturesResponse
{
    [JsonPropertyName("fixtures")]
    public FotMobFixturesWrapper? Fixtures { get; set; }
}

public class FotMobFixturesWrapper
{
    [JsonPropertyName("allMatches")]
    public List<FotMobFixtureSummary> AllMatches { get; set; } = [];
}

public class FotMobFixtureSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("home")]
    public FotMobTeamRef Home { get; set; } = new();

    [JsonPropertyName("away")]
    public FotMobTeamRef Away { get; set; } = new();
}

public class FotMobTeamRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class FotMobMatchDetailsResponse
{
    [JsonPropertyName("content")]
    public FotMobContent? Content { get; set; }
}

public class FotMobContent
{
    [JsonPropertyName("lineup")]
    public FotMobLineup? Lineup { get; set; }
}

public class FotMobLineup
{
    [JsonPropertyName("lineupType")]
    public string? LineupType { get; set; }

    [JsonPropertyName("homeTeam")]
    public FotMobTeamLineup HomeTeam { get; set; } = new();

    [JsonPropertyName("awayTeam")]
    public FotMobTeamLineup AwayTeam { get; set; } = new();
}

public class FotMobTeamLineup
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("formation")]
    public string? Formation { get; set; }

    [JsonPropertyName("starters")]
    public List<FotMobPlayer> Starters { get; set; } = [];

    [JsonPropertyName("subs")]
    public List<FotMobPlayer> Subs { get; set; } = [];

    [JsonPropertyName("unavailable")]
    public List<FotMobUnavailablePlayer> Unavailable { get; set; } = [];
}

public class FotMobPlayer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shirtNumber")]
    public string? ShirtNumber { get; set; }

    [JsonPropertyName("usualPlayingPositionId")]
    public int? UsualPlayingPositionId { get; set; }

    [JsonPropertyName("isCaptain")]
    public bool IsCaptain { get; set; }

    // Left-right position within the player's line on FotMob's horizontally-oriented pitch graphic
    // (x = distance from own goal, y = position across the pitch width). Used to order players
    // within a formation line the same way FotMob's own lineup graphic does.
    [JsonPropertyName("horizontalLayout")]
    public FotMobLayout? HorizontalLayout { get; set; }
}

public class FotMobLayout
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

public class FotMobUnavailablePlayer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("unavailability")]
    public FotMobUnavailability? Unavailability { get; set; }
}

public class FotMobUnavailability
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("expectedReturn")]
    public string? ExpectedReturn { get; set; }
}
