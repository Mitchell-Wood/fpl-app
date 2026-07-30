using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>A manager's squad for one gameweek, from /entry/{id}/event/{event}/picks/.</summary>
public class TeamPicks
{
    [JsonPropertyName("active_chip")]
    public string? ActiveChip { get; set; }

    [JsonPropertyName("entry_history")]
    public EntryHistory EntryHistory { get; set; } = new();

    [JsonPropertyName("picks")]
    public List<Pick> Picks { get; set; } = [];
}

public class EntryHistory
{
    [JsonPropertyName("event")]
    public int Event { get; set; }

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("total_points")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("bank")]
    public int Bank { get; set; }

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("event_transfers")]
    public int EventTransfers { get; set; }

    [JsonPropertyName("event_transfers_cost")]
    public int EventTransfersCost { get; set; }

    [JsonPropertyName("points_on_bench")]
    public int PointsOnBench { get; set; }
}

public class Pick
{
    [JsonPropertyName("element")]
    public int Element { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("multiplier")]
    public int Multiplier { get; set; }

    [JsonPropertyName("is_captain")]
    public bool IsCaptain { get; set; }

    [JsonPropertyName("is_vice_captain")]
    public bool IsViceCaptain { get; set; }
}
