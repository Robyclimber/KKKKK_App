using System.Collections.ObjectModel;

namespace RuoteLab.Models;

public sealed class CircuitDefinition
{
    public int Id { get; set; }

    public string CircuitId { get; set; } = string.Empty;

    public string RoomName { get; set; } = "Sala Arrampicata";

    public required string Name { get; set; }

    public string Difficulty { get; set; } = string.Empty;

    public string Inclination { get; set; } = string.Empty;

    public bool SuggestNextHoldEnabled { get; set; }

    public string WallName { get; set; } = string.Empty;

    public CircuitGlobalsDefinition Globals { get; set; } = new();

    public ObservableCollection<CircuitMovementDefinition> Movements { get; } = new();

    public string DisplayLabel =>
        $"{Name} - Diff {Difficulty} - Incl. {Inclination} - Suggerisci {(SuggestNextHoldEnabled ? "SI" : "NO")} - Movimenti {Movements.Count}";
}
