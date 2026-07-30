namespace RouteLab.Models;

public sealed class NextHoldSuggestionCandidate
{
    public int HoleNumber { get; init; }

    public HoldType HoldType { get; init; }

    public string MovementDirection { get; init; } = string.Empty;

    public ClimbingMovementPlan MovementPlan { get; init; } = new();

    public double Score { get; init; }

    public double DistanceFromMovingHand { get; init; }

    public double DistanceFromCenter { get; init; }

    public double CenterShiftRequired { get; init; }

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

    public double ExtensionRatio { get; init; }

    public double WallDifficulty { get; init; }

    public string PrimaryReason { get; init; } = string.Empty;

    public string SecondaryReason { get; init; } = string.Empty;
}

