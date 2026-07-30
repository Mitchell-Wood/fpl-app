namespace FplApp.Core.Recommendations;

/// <summary>One side of a transfer: the player leaving and the player coming in.</summary>
public class TransferLeg
{
    public int OutPlayerId { get; set; }
    public string OutWebName { get; set; } = string.Empty;
    public string OutTeamName { get; set; } = string.Empty;
    public int OutNowCost { get; set; }

    public int InPlayerId { get; set; }
    public string InWebName { get; set; } = string.Empty;
    public string InTeamName { get; set; } = string.Empty;
    public int InNowCost { get; set; }
    public double InForm { get; set; }
    public int InTotalPoints { get; set; }
}

/// <summary>
/// A two-transfer plan: take a slight downgrade on one squad player to free up money, then put
/// that money (plus bank) toward a bigger upgrade on another squad player than their own price
/// alone could afford.
/// </summary>
public class FundedUpgradeSuggestion
{
    public TransferLeg Downgrade { get; set; } = new();
    public TransferLeg Upgrade { get; set; } = new();

    /// <summary>Tenths of a million freed up by the downgrade leg.</summary>
    public int MoneySaved { get; set; }
}
