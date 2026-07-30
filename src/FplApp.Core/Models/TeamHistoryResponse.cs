using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>Response from /entry/{id}/history/. Past-gameweek data, always public.</summary>
public class TeamHistoryResponse
{
    [JsonPropertyName("current")]
    public List<EntryHistory> Current { get; set; } = [];

    [JsonPropertyName("chips")]
    public List<ChipPlay> Chips { get; set; } = [];
}

public class ChipPlay
{
    /// <summary>"wildcard", "freehit", "bboost", or "3xc".</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("event")]
    public int Event { get; set; }
}
