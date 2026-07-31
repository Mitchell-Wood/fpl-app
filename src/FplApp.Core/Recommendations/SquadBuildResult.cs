namespace FplApp.Core.Recommendations;

/// <summary>A freshly built 15-man squad (e.g. for a Wildcard or Free Hit), plus its best starting XI.</summary>
public class SquadBuildResult
{
    /// <summary>The budget the squad was built to, in tenths of a million.</summary>
    public int Budget { get; set; }

    /// <summary>Total cost of all 15 players, in tenths of a million.</summary>
    public int TotalCost { get; set; }

    /// <summary>Budget left unspent, in tenths of a million.</summary>
    public int BudgetRemaining { get; set; }

    /// <summary>The best-scoring valid formation found for the starting XI, e.g. "3-4-3".</summary>
    public string Formation { get; set; } = string.Empty;

    /// <summary>Combined projected points of the starting XI over the fixture lookahead window (captain not doubled).</summary>
    public double StartingElevenProjectedPoints { get; set; }

    /// <summary>All 15 players, flagged for whether they're in the starting XI and who's captain.</summary>
    public List<SquadBuilderPlayer> Players { get; set; } = [];
}

public class SquadBuilderPlayer
{
    public int PlayerId { get; set; }
    public string WebName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int ElementType { get; set; }
    public int NowCost { get; set; }

    /// <summary>Projected points over the fixture lookahead window used to build the squad.</summary>
    public double ProjectedPoints { get; set; }

    public bool IsStarting { get; set; }
    public bool IsCaptain { get; set; }
}
