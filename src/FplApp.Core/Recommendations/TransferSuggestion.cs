namespace FplApp.Core.Recommendations;

/// <summary>A flagged squad player paired with affordable same-position alternatives.</summary>
public class TransferSuggestion
{
    public int OutPlayerId { get; set; }
    public string OutWebName { get; set; } = string.Empty;
    public string OutTeamName { get; set; } = string.Empty;
    public List<string> OutFlags { get; set; } = [];

    /// <summary>
    /// Bank plus the outgoing player's current price (tenths of a million) — an approximation of
    /// what's available for the swap. This isn't necessarily your exact FPL sell price, which can
    /// be lower than the current price once a player has risen in value (FPL only refunds half of
    /// any profit).
    /// </summary>
    public int BudgetAvailable { get; set; }

    /// <summary>
    /// Estimated points gained over the fixture lookahead window by swapping to the top candidate,
    /// projected from form (or FPL's expected points as a fallback) scaled by each upcoming
    /// fixture's difficulty. Used to rank suggestions and judge whether a transfer — free or a hit
    /// — is actually worth making.
    /// </summary>
    public double ExpectedPointsGain { get; set; }

    public List<TransferCandidate> Candidates { get; set; } = [];
}

public class TransferCandidate
{
    public int PlayerId { get; set; }
    public string WebName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int NowCost { get; set; }
    public double Form { get; set; }
    public int TotalPoints { get; set; }
}
