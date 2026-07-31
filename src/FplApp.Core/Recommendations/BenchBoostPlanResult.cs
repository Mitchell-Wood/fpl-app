namespace FplApp.Core.Recommendations;

/// <summary>
/// A transfer plan aimed at maximizing your full 15-man squad's points for one specific gameweek —
/// the metric that matters when Bench Boost is active, since every player scores that week.
/// </summary>
public class BenchBoostPlanResult
{
    public int EventId { get; set; }

    /// <summary>Your current squad's total projected points for the target gameweek, as-is.</summary>
    public double CurrentSquadProjectedPoints { get; set; }

    /// <summary>Projected total for the target gameweek after making <see cref="Plan"/>'s recommended transfers.</summary>
    public double ProjectedSquadPointsAfterTransfers { get; set; }

    public TransferPlanResult Plan { get; set; } = new();

    /// <summary>
    /// The resulting 15 once <see cref="Plan"/>'s recommended transfers are applied — every player
    /// marked as starting, since Bench Boost counts the whole squad, not just the usual XI.
    /// </summary>
    public List<SquadBuilderPlayer> ProjectedSquad { get; set; } = [];
}
