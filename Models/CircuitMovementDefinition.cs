namespace RouteLab.Models;

public sealed class CircuitMovementDefinition
{
    public required string WallName { get; init; }

    public int HoleNumber { get; init; }

    public HandSide Hand { get; init; }

    public MovementRole Role { get; init; }

    public int Sequence { get; init; }

    public bool IsFootHold => Role == MovementRole.Feet;

    public string Summary => IsFootHold
        ? $"Piedi - Foro {HoleNumber}"
        : $"{Sequence:00} - {GetHandLabel()} - {GetRoleLabel()} - Foro {HoleNumber}";

    public string GetHandLabel() => IsFootHold
        ? "Piedi"
        : Hand == HandSide.Left ? "Mano SX" : "Mano DX";

    public string GetRoleLabel() =>
        Role switch
        {
            MovementRole.Start => "Start",
            MovementRole.Top => "Top",
            MovementRole.Feet => "Piedi",
            _ => "Movimento"
        };
}

