using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>A single fixture from the /fixtures/ endpoint.</summary>
public class Fixture
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("event")]
    public int? Event { get; set; }

    [JsonPropertyName("finished")]
    public bool Finished { get; set; }

    [JsonPropertyName("finished_provisional")]
    public bool FinishedProvisional { get; set; }

    [JsonPropertyName("kickoff_time")]
    public DateTimeOffset? KickoffTime { get; set; }

    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }

    [JsonPropertyName("started")]
    public bool? Started { get; set; }

    [JsonPropertyName("team_a")]
    public int TeamA { get; set; }

    [JsonPropertyName("team_a_score")]
    public int? TeamAScore { get; set; }

    [JsonPropertyName("team_h")]
    public int TeamH { get; set; }

    [JsonPropertyName("team_h_score")]
    public int? TeamHScore { get; set; }

    [JsonPropertyName("team_h_difficulty")]
    public int TeamHDifficulty { get; set; }

    [JsonPropertyName("team_a_difficulty")]
    public int TeamADifficulty { get; set; }

    [JsonPropertyName("pulse_id")]
    public int PulseId { get; set; }

    /// <summary>
    /// Live/final match stats broken down by identifier (e.g. "bps", "goals_scored"), each split
    /// into home and away scorers. Populated once a fixture has kicked off — updates in real time
    /// during play, same as the official site.
    /// </summary>
    [JsonPropertyName("stats")]
    public List<FixtureStat> Stats { get; set; } = [];
}

public class FixtureStat
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = "";

    [JsonPropertyName("a")]
    public List<FixtureStatValue> Away { get; set; } = [];

    [JsonPropertyName("h")]
    public List<FixtureStatValue> Home { get; set; } = [];
}

public class FixtureStatValue
{
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("element")]
    public int Element { get; set; }
}
