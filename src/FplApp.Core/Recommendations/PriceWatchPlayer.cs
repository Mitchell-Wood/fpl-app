namespace FplApp.Core.Recommendations;

/// <summary>A player's price-change and transfer-momentum snapshot.</summary>
public class PriceWatchPlayer
{
    public int PlayerId { get; set; }
    public string WebName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int ElementType { get; set; }
    public int NowCost { get; set; }
    public double SelectedByPercent { get; set; }

    /// <summary>Price change so far today, in tenths of a million (e.g. 1 = risen £0.1m).</summary>
    public int CostChangeEventTenths { get; set; }

    public int TransfersInEvent { get; set; }
    public int TransfersOutEvent { get; set; }
    public int NetTransfersEvent { get; set; }
}
