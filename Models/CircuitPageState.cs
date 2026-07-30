namespace RuoteLab.Models;

public sealed class CircuitPageState
{
    public string WorkflowTitleText { get; init; } = "Circuiti";

    public string WorkflowMessageText { get; init; } = "Seleziona una sala per vedere i circuiti disponibili.";

    public string CurrentWallLabel { get; init; } = "Nessuna parete disponibile.";

    public IReadOnlyList<string> AvailableRooms { get; init; } = Array.Empty<string>();

    public string? SelectedRoomName { get; init; }

    public IReadOnlyList<WallDefinition> VisibleWalls { get; init; } = Array.Empty<WallDefinition>();

    public WallDefinition? SelectedWall { get; init; }

    public bool CanCreateCircuit { get; init; }

    public bool CanUpdateCircuit { get; init; }

    public bool CanDeleteCircuit { get; init; }

    public bool CanPickWall { get; init; } = true;

    public string EditorModeText { get; init; } = "Nuovo circuito";

    public string CircuitSummaryText { get; init; } = "Nessun circuito selezionato.";

    public IReadOnlyList<CircuitDefinition> VisibleCircuits { get; init; } = Array.Empty<CircuitDefinition>();

    public IReadOnlyList<CircuitMovementDefinition> OrderedMovements { get; init; } = Array.Empty<CircuitMovementDefinition>();

    public string InteractionHintText { get; init; } = string.Empty;
}
