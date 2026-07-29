using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>Root response of the /bootstrap-static/ endpoint.</summary>
public class BootstrapStatic
{
    [JsonPropertyName("events")]
    public List<Event> Events { get; set; } = new();

    [JsonPropertyName("teams")]
    public List<Team> Teams { get; set; } = new();

    [JsonPropertyName("element_types")]
    public List<ElementType> ElementTypes { get; set; } = new();

    [JsonPropertyName("elements")]
    public List<Player> Elements { get; set; } = new();

    [JsonPropertyName("total_players")]
    public int TotalPlayers { get; set; }
}
