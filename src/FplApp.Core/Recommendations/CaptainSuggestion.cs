namespace FplApp.Core.Recommendations;

/// <summary>A starting-XI player ranked by expected points if made captain this gameweek.</summary>
public class CaptainSuggestion
{
    public int PlayerId { get; set; }
    public string WebName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public bool IsCurrentCaptain { get; set; }
    public bool IsCurrentViceCaptain { get; set; }
    public double ExpectedPoints { get; set; }
    public List<CaptainFixture> Fixtures { get; set; } = [];

    /// <summary>"Blank gameweek" or "Double gameweek", if applicable.</summary>
    public string? Note { get; set; }
}

public class CaptainFixture
{
    public string Opponent { get; set; } = string.Empty;

    /// <summary>"H" or "A".</summary>
    public string Venue { get; set; } = string.Empty;

    public int Difficulty { get; set; }
}
