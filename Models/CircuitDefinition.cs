using System.Collections.ObjectModel;

namespace RouteLab.Models;

public sealed class CircuitDefinition
{
    public int Id { get; set; }

    public string CircuitId { get; set; } = string.Empty;

    public string RoomName { get; set; } = "Sala Arrampicata";

    public required string Name { get; set; }

    public string Difficulty { get; set; } = string.Empty;

    public string Inclination { get; set; } = string.Empty;

    public string ClimberProfileId { get; set; } = ClimberProfileDefinition.DefaultProfileId;

    public bool SuggestNextHoldEnabled { get; set; }

    public string WallName { get; set; } = string.Empty;

    public ObservableCollection<string> WallNames { get; } = new();

    public CircuitGlobalsDefinition Globals { get; set; } = new();

    public ObservableCollection<CircuitMovementDefinition> Movements { get; } = new();

    public int FootHoldCount => Movements.Count(movement => movement.IsFootHold);

    public int DynamicMovementCount => Movements.Count - FootHoldCount;

    public IReadOnlyList<string> GetWallNames()
    {
        var names = WallNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (names.Count == 0 && !string.IsNullOrWhiteSpace(WallName))
        {
            names.Add(WallName.Trim());
        }

        return names;
    }

    public bool UsesWall(string? wallName)
    {
        return !string.IsNullOrWhiteSpace(wallName) &&
               GetWallNames().Contains(wallName.Trim(), StringComparer.Ordinal);
    }

    public void SetWallNames(IEnumerable<string> wallNames)
    {
        ArgumentNullException.ThrowIfNull(wallNames);

        var normalizedNames = wallNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        WallNames.Clear();
        foreach (var name in normalizedNames)
        {
            WallNames.Add(name);
        }

        WallName = normalizedNames.FirstOrDefault() ?? string.Empty;
    }

    public string WallSummary => string.Join(", ", GetWallNames());

    public string DisplayLabel =>
        $"{Name} - {GetWallCountLabel()} - Diff {Difficulty} - Incl. {Inclination} - Movimenti {DynamicMovementCount} - Piedi {FootHoldCount}";

    private string GetWallCountLabel()
    {
        var count = GetWallNames().Count;
        return count == 1 ? "1 parete" : $"{count} pareti";
    }
}

