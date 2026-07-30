using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>Basic manager/team info from the /entry/{id}/ endpoint.</summary>
public class TeamEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("player_first_name")]
    public string PlayerFirstName { get; set; } = string.Empty;

    [JsonPropertyName("player_last_name")]
    public string PlayerLastName { get; set; } = string.Empty;

    [JsonPropertyName("summary_overall_points")]
    public int? SummaryOverallPoints { get; set; }

    [JsonPropertyName("summary_overall_rank")]
    public int? SummaryOverallRank { get; set; }

    [JsonPropertyName("current_event")]
    public int? CurrentEvent { get; set; }

    [JsonPropertyName("last_deadline_bank")]
    public int? LastDeadlineBank { get; set; }

    [JsonPropertyName("last_deadline_value")]
    public int? LastDeadlineValue { get; set; }
}
