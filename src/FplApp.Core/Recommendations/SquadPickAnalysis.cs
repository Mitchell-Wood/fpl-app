namespace FplApp.Core.Recommendations;

/// <summary>One player in a manager's squad, combined with availability/form/fixture context.</summary>
public class SquadPickAnalysis
{
    public int PlayerId { get; set; }
    public string WebName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int ElementType { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsViceCaptain { get; set; }
    public int Multiplier { get; set; }
    public bool IsBenched { get; set; }
    public int NowCost { get; set; }
    public double Form { get; set; }
    public int TotalPoints { get; set; }
    public double AvgUpcomingDifficulty { get; set; }

    /// <summary>Projected points for the next gameweek, used to recommend a starting XI/captain.</summary>
    public double ExpectedPointsNextGameweek { get; set; }

    /// <summary>This player's fixture(s) for the next gameweek — empty for a blank, two entries
    /// for a double gameweek.</summary>
    public List<CaptainFixture> NextFixtures { get; set; } = [];

    /// <summary>Human-readable flags, e.g. "Injured", "Poor recent form", "Tough fixtures ahead".</summary>
    public List<string> Flags { get; set; } = [];
}
