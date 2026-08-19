namespace FplApp.Api.Models;

/// <summary>Predicted/confirmed starting lineup for one FPL fixture, sourced from FotMob.</summary>
public class PredictedLineup
{
    public int FixtureId { get; set; }
    public DateTimeOffset? KickoffTime { get; set; }

    /// <summary>"predicted", "confirmed", "lastKnownLineup", or "unavailable".</summary>
    public string Status { get; set; } = "unavailable";

    public LineupTeam? HomeTeam { get; set; }
    public LineupTeam? AwayTeam { get; set; }
}

public class LineupTeam
{
    public string Name { get; set; } = string.Empty;
    public string? Formation { get; set; }
    public List<LineupPlayer> Starters { get; set; } = [];
    public List<LineupPlayer> Subs { get; set; } = [];
    public List<UnavailablePlayer> Unavailable { get; set; } = [];
}

public class LineupPlayer
{
    public string Name { get; set; } = string.Empty;
    public string? ShirtNumber { get; set; }

    /// <summary>GK, DEF, MID, or FWD.</summary>
    public string Position { get; set; } = string.Empty;
    public bool IsCaptain { get; set; }
}

public class UnavailablePlayer
{
    public string Name { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
