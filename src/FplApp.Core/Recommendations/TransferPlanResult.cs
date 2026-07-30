namespace FplApp.Core.Recommendations;

/// <summary>
/// Answers "how many of my free transfers should I actually use this week" — picks the best
/// affordable combination of transfers up to the number of free transfers available, and separately
/// checks whether one further transfer would be worth a -4 point hit.
/// </summary>
public class TransferPlanResult
{
    public int FreeTransfersAvailable { get; set; }
    public int FreeTransfersUsed { get; set; }

    /// <summary>Free transfers left unused — worth banking for future flexibility (injuries, a bigger rebuild) rather than spending on marginal upgrades.</summary>
    public int FreeTransfersToBank { get; set; }

    /// <summary>The free transfers worth making, best (biggest points gain) first.</summary>
    public List<TransferSuggestion> RecommendedTransfers { get; set; } = [];

    /// <summary>Combined projected points gain from making all of <see cref="RecommendedTransfers"/>.</summary>
    public double TotalExpectedPointsGain { get; set; }

    /// <summary>The next-best transfer beyond your free ones, if any — would cost a -4 hit.</summary>
    public TransferSuggestion? HitCandidate { get; set; }

    /// <summary><see cref="HitCandidate"/>'s projected points gain minus the 4-point hit cost.</summary>
    public double? HitCandidateNetGain { get; set; }

    /// <summary>True when the hit candidate's projected gain exceeds the 4-point cost of taking it.</summary>
    public bool HitWorthIt { get; set; }
}
