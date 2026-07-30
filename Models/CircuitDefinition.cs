using System.Collections.ObjectModel;

namespace WallPanelPlanner.Models;

public sealed class CircuitDefinition
{
    public int Id { get; set; }

    public string RoomName { get; set; } = "Sala Arrampicata";

    public required string Name { get; set; }

    public string Difficulty { get; set; } = string.Empty;

    public string Inclination { get; set; } = string.Empty;

    public string WallName { get; set; } = string.Empty;

    public ObservableCollection<CircuitMovementDefinition> Movements { get; } = new();

    public string DisplayLabel =>
        $"{Name} - Diff {Difficulty} - Incl. {Inclination} - Movimenti {Movements.Count}";
}
