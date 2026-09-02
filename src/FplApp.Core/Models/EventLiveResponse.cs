using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>Response from /event/{id}/live/ — each player's live stats for one gameweek.</summary>
public class EventLiveResponse
{
    [JsonPropertyName("elements")]
    public List<EventLiveElement> Elements { get; set; } = [];
}

public class EventLiveElement
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("stats")]
    public EventLiveElementStats Stats { get; set; } = new();
}

public class EventLiveElementStats
{
    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }

    [JsonPropertyName("total_points")]
    public int TotalPoints { get; set; }
}
