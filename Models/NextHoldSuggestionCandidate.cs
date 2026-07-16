namespace RuoteLab.Models;

public sealed class NextHoldSuggestionCandidate
{
    public int HoleNumber { get; init; }

    public HoldType HoldType { get; init; }

    public string MovementDirection { get; init; } = string.Empty;

    public double Score { get; init; }

    public double DistanceFromMovingHand { get; init; }

    public double DistanceFromCenter { get; init; }

    public double CenterShiftRequired { get; init; }

    public double CenterConfidence { get; init; }

    public string CenterConfidenceLabel { get; init; } = string.Empty;

    public double ExtensionRatio { get; init; }

    public double WallDifficulty { get; init; }

    public string PrimaryReason { get; init; } = string.Empty;

    public string SecondaryReason { get; init; } = string.Empty;
}
