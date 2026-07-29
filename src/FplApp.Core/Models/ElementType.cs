using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>A player position, e.g. Goalkeeper, Defender, Midfielder, Forward.</summary>
public class ElementType
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("singular_name")]
    public string SingularName { get; set; } = string.Empty;

    [JsonPropertyName("singular_name_short")]
    public string SingularNameShort { get; set; } = string.Empty;

    [JsonPropertyName("plural_name")]
    public string PluralName { get; set; } = string.Empty;
}
