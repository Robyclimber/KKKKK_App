namespace RuoteLab.Models;

public sealed class NextHoldSuggestionRequest
{
    public required WallDefinition Wall { get; init; }

    public CircuitDefinition? Circuit { get; init; }

    public HandSide MovingHand { get; init; }

    public int CurrentLeftHandHoleNumber { get; init; }

    public int CurrentRightHandHoleNumber { get; init; }

    public int? CurrentLeftFootHoleNumber { get; init; }

    public int? CurrentRightFootHoleNumber { get; init; }

    public double? CenterX { get; init; }

    public double? CenterY { get; init; }

    public double? WallAngleDegreesOverride { get; init; }

    public bool ExcludeCurrentHandHoles { get; init; } = true;

    public bool ExcludeFootOnlyHoldsForHands { get; init; } = true;

    public int MaxSuggestions { get; init; } = 3;
}
