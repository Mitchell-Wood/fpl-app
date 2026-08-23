using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>A single transfer from /entry/{id}/transfers/.</summary>
public class TeamTransfer
{
    [JsonPropertyName("element_in")]
    public int ElementIn { get; set; }

    [JsonPropertyName("element_in_cost")]
    public int ElementInCost { get; set; }

    [JsonPropertyName("element_out")]
    public int ElementOut { get; set; }

    [JsonPropertyName("element_out_cost")]
    public int ElementOutCost { get; set; }

    [JsonPropertyName("entry")]
    public int Entry { get; set; }

    [JsonPropertyName("event")]
    public int Event { get; set; }

    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; set; }
}
