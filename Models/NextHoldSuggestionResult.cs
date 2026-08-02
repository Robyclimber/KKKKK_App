namespace RouteLab.Models;

public sealed class NextHoldSuggestionResult
{
    public int? SuggestedHoleNumber { get; init; }

    public string SuggestedDirection { get; init; } = string.Empty;

    public ClimbingMovementPlan MovementPlan { get; init; } = new();

    public string PrimaryReason { get; init; } = string.Empty;

    public string SecondaryReason { get; init; } = string.Empty;

    public double CenterConfidence { get; init; }

    public string CenterConfidenceLabel { get; init; } = string.Empty;

    public IReadOnlyList<int> SupportFootHoleNumbers { get; init; } = Array.Empty<int>();

    public IReadOnlyList<int> CurrentSupportFootHoleNumbers { get; init; } = Array.Empty<int>();

    public IReadOnlyList<int> FootMoveHoleNumbers { get; init; } = Array.Empty<int>();

    public double FootRepositionDistance { get; init; }

    public bool PreparationCenterInsideSupportTriangle { get; init; }

    public double PreparationDistanceFromSupportTriangle { get; init; }

    public bool CenterInsideSupportTriangle { get; init; }

    public double DistanceFromSupportTriangle { get; init; }

    public double BiomechanicalCenterX { get; init; }

    public double BiomechanicalCenterY { get; init; }

    public double GravityTorqueNewtonMeter { get; init; }

    public double NormalGravityForceNewton { get; init; }

    public double ReachPenalty { get; init; }

    public bool IsReachFeasible { get; init; }

    public IReadOnlyList<NextHoldSuggestionCandidate> Candidates { get; init; } = Array.Empty<NextHoldSuggestionCandidate>();
}

