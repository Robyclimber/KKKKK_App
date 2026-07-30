namespace RouteLab.Models;

public sealed class NextHoldSuggestionRequest
{
    public required WallDefinition Wall { get; init; }

    public ClimberProfileDefinition ClimberProfile { get; init; } = new();

    public CircuitDefinition? Circuit { get; init; }

    public HandSide MovingHand { get; init; }

    public int CurrentLeftHandHoleNumber { get; init; }

    public int CurrentRightHandHoleNumber { get; init; }

    public IReadOnlyList<int> CurrentFootHoleNumbers { get; init; } = Array.Empty<int>();

    public double? CenterX { get; init; }

    public double? CenterY { get; init; }

    public double? WallAngleDegreesOverride { get; init; }

    public bool ExcludeCurrentHandHoles { get; init; } = true;

    public bool ExcludeFootOnlyHoldsForHands { get; init; } = true;

    public int MaxSuggestions { get; init; } = 3;
}

