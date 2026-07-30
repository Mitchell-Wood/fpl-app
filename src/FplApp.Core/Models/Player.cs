using System.Text.Json.Serialization;

namespace FplApp.Core.Models;

/// <summary>A player, referred to as "element" by the FPL API.</summary>
public class Player
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("second_name")]
    public string SecondName { get; set; } = string.Empty;

    [JsonPropertyName("web_name")]
    public string WebName { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public int Team { get; set; }

    [JsonPropertyName("element_type")]
    public int ElementType { get; set; }

    [JsonPropertyName("now_cost")]
    public int NowCost { get; set; }

    [JsonPropertyName("total_points")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("form")]
    public string Form { get; set; } = string.Empty;

    [JsonPropertyName("points_per_game")]
    public string PointsPerGame { get; set; } = string.Empty;

    [JsonPropertyName("selected_by_percent")]
    public string SelectedByPercent { get; set; } = string.Empty;

    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }

    [JsonPropertyName("goals_scored")]
    public int GoalsScored { get; set; }

    [JsonPropertyName("assists")]
    public int Assists { get; set; }

    [JsonPropertyName("clean_sheets")]
    public int CleanSheets { get; set; }

    [JsonPropertyName("goals_conceded")]
    public int GoalsConceded { get; set; }

    [JsonPropertyName("own_goals")]
    public int OwnGoals { get; set; }

    [JsonPropertyName("penalties_saved")]
    public int PenaltiesSaved { get; set; }

    [JsonPropertyName("penalties_missed")]
    public int PenaltiesMissed { get; set; }

    [JsonPropertyName("yellow_cards")]
    public int YellowCards { get; set; }

    [JsonPropertyName("red_cards")]
    public int RedCards { get; set; }

    [JsonPropertyName("saves")]
    public int Saves { get; set; }

    [JsonPropertyName("bonus")]
    public int Bonus { get; set; }

    [JsonPropertyName("bps")]
    public int Bps { get; set; }

    [JsonPropertyName("influence")]
    public string Influence { get; set; } = string.Empty;

    [JsonPropertyName("creativity")]
    public string Creativity { get; set; } = string.Empty;

    [JsonPropertyName("threat")]
    public string Threat { get; set; } = string.Empty;

    [JsonPropertyName("ict_index")]
    public string IctIndex { get; set; } = string.Empty;

    [JsonPropertyName("expected_goals")]
    public string ExpectedGoals { get; set; } = string.Empty;

    [JsonPropertyName("expected_assists")]
    public string ExpectedAssists { get; set; } = string.Empty;

    [JsonPropertyName("expected_goal_involvements")]
    public string ExpectedGoalInvolvements { get; set; } = string.Empty;

    [JsonPropertyName("expected_goals_conceded")]
    public string ExpectedGoalsConceded { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("news")]
    public string News { get; set; } = string.Empty;

    [JsonPropertyName("chance_of_playing_next_round")]
    public int? ChanceOfPlayingNextRound { get; set; }

    [JsonPropertyName("transfers_in_event")]
    public int TransfersInEvent { get; set; }

    [JsonPropertyName("transfers_out_event")]
    public int TransfersOutEvent { get; set; }

    /// <summary>Price change so far today, in tenths of a million (e.g. 1 = risen £0.1m).</summary>
    [JsonPropertyName("cost_change_event")]
    public int CostChangeEvent { get; set; }

    /// <summary>Price change since the start of the season, in tenths of a million.</summary>
    [JsonPropertyName("cost_change_start")]
    public int CostChangeStart { get; set; }
}
