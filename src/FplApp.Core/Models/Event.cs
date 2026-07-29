using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>A gameweek.</summary>
public class Event
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("deadline_time")]
    public DateTimeOffset DeadlineTime { get; set; }

    [JsonPropertyName("finished")]
    public bool Finished { get; set; }

    [JsonPropertyName("is_previous")]
    public bool IsPrevious { get; set; }

    [JsonPropertyName("is_current")]
    public bool IsCurrent { get; set; }

    [JsonPropertyName("is_next")]
    public bool IsNext { get; set; }

    [JsonPropertyName("average_entry_score")]
    public int AverageEntryScore { get; set; }

    [JsonPropertyName("highest_score")]
    public int? HighestScore { get; set; }
}
