using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

public class Team
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("short_name")]
    public string ShortName { get; set; } = string.Empty;

    [JsonPropertyName("strength")]
    public int? Strength { get; set; }

    [JsonPropertyName("strength_overall_home")]
    public int StrengthOverallHome { get; set; }

    [JsonPropertyName("strength_overall_away")]
    public int StrengthOverallAway { get; set; }

    [JsonPropertyName("strength_attack_home")]
    public int StrengthAttackHome { get; set; }

    [JsonPropertyName("strength_attack_away")]
    public int StrengthAttackAway { get; set; }

    [JsonPropertyName("strength_defence_home")]
    public int StrengthDefenceHome { get; set; }

    [JsonPropertyName("strength_defence_away")]
    public int StrengthDefenceAway { get; set; }

    [JsonPropertyName("played")]
    public int Played { get; set; }

    [JsonPropertyName("win")]
    public int Win { get; set; }

    [JsonPropertyName("draw")]
    public int Draw { get; set; }

    [JsonPropertyName("loss")]
    public int Loss { get; set; }

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}
