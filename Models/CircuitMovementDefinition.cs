namespace WallPanelPlanner.Models;

public sealed class CircuitMovementDefinition
{
    public required string WallName { get; init; }

    public int HoleNumber { get; init; }

    public HandSide Hand { get; init; }

    public MovementRole Role { get; init; }

    public int Sequence { get; init; }

    public string Summary => $"{Sequence:00} - {GetHandLabel()} - {GetRoleLabel()} - Foro {HoleNumber}";

    public string GetHandLabel() => Hand == HandSide.Left ? "Mano SX" : "Mano DX";

    public string GetRoleLabel() =>
        Role switch
        {
            MovementRole.Start => "Start",
            MovementRole.Top => "Top",
            _ => "Movimento"
        };
}
