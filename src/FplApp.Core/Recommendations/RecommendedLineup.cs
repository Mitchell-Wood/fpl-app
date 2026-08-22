namespace FplApp.Core.Recommendations;

/// <summary>The best legal starting XI (plus captain/vice) from an existing 15-man squad for one
/// specific gameweek, alongside each player's projected points for that gameweek.</summary>
public class RecommendedLineup
{
    /// <summary>e.g. "4-4-2".</summary>
    public string Formation { get; set; } = string.Empty;

    /// <summary>Every squad player's recommendation, keyed by player (element) id.</summary>
    public Dictionary<int, RecommendedLineupPlayer> ByPlayerId { get; set; } = [];
}

public class RecommendedLineupPlayer
{
    public bool IsStarting { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsViceCaptain { get; set; }
    public double ExpectedPoints { get; set; }
}
