using RouteLab.Models;

namespace RouteLab.Services;

public sealed class CircuitEditingService : ICircuitEditingService
{
    private readonly IAppSettingsService appSettingsService;

    public CircuitEditingService()
        : this(new AppSettingsService())
    {
    }

    public CircuitEditingService(IAppSettingsService appSettingsService)
    {
        this.appSettingsService = appSettingsService;
    }

    public CircuitDefinition CreateCircuit(string? name, string? difficulty, string? inclination, string? climberProfileId, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals, IReadOnlyList<WallDefinition> walls, string fallbackName)
    {
        ArgumentNullException.ThrowIfNull(walls);
        ValidateWalls(walls);

        var defaults = appSettingsService.Load().CircuitDefaults;
        var effectiveGlobals = globals is null ? defaults : CloneGlobals(globals);
        var primaryWall = walls[0];

        var circuit = new CircuitDefinition
        {
            CircuitId = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(name) ? fallbackName : name.Trim(),
            Difficulty = difficulty?.Trim() ?? string.Empty,
            Inclination = inclination?.Trim() ?? string.Empty,
            ClimberProfileId = NormalizeClimberProfileId(climberProfileId),
            SuggestNextHoldEnabled = suggestNextHoldEnabled,
            RoomName = primaryWall.RoomName,
            Globals = CloneGlobals(effectiveGlobals)
        };
        circuit.SetWallNames(walls.Select(wall => wall.Name));
        return circuit;
    }

    public void UpdateCircuitMetadata(CircuitDefinition circuit, string? name, string? difficulty, string? inclination, string? climberProfileId, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        circuit.Name = string.IsNullOrWhiteSpace(name) ? circuit.Name : name.Trim();
        circuit.Difficulty = difficulty?.Trim() ?? string.Empty;
        circuit.Inclination = inclination?.Trim() ?? string.Empty;
        circuit.ClimberProfileId = NormalizeClimberProfileId(climberProfileId);
        circuit.SuggestNextHoldEnabled = suggestNextHoldEnabled;
        if (globals is not null)
        {
            circuit.Globals = CloneGlobals(globals);
        }
    }

    public void UpdateCircuitWalls(CircuitDefinition circuit, IReadOnlyList<WallDefinition> walls)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(walls);
        ValidateWalls(walls);

        var selectedNames = walls
            .Select(wall => wall.Name)
            .ToHashSet(StringComparer.Ordinal);
        var removedWallsWithMovements = circuit.Movements
            .Where(movement => !selectedNames.Contains(movement.WallName))
            .Select(movement => movement.WallName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name)
            .ToList();
        if (removedWallsWithMovements.Count > 0)
        {
            throw new InvalidOperationException(
                $"Non puoi rimuovere le pareti {string.Join(", ", removedWallsWithMovements)}: contengono movimenti del circuito.");
        }

        circuit.RoomName = walls[0].RoomName;
        circuit.SetWallNames(walls.Select(wall => wall.Name));
    }

    public void ToggleMovement(CircuitDefinition circuit, string wallName, WallHoleDefinition hole, HandSide hand, MovementRole role)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        if (role == MovementRole.Feet)
        {
            ToggleFootHold(circuit, wallName, hole);
            return;
        }

        var existing = circuit.Movements
            .FirstOrDefault(m =>
                m.WallName == wallName &&
                m.HoleNumber == hole.Number &&
                m.Hand == hand &&
                !m.IsFootHold);

        if (existing is not null)
        {
            circuit.Movements.Remove(existing);
            Resequence(circuit);
            return;
        }

        if (role == MovementRole.Start)
        {
            foreach (var movement in circuit.Movements.Where(m => m.Hand == hand && m.Role == MovementRole.Start).ToList())
            {
                circuit.Movements.Remove(movement);
            }
        }

        if (role == MovementRole.Top)
        {
            foreach (var movement in circuit.Movements.Where(m => m.Hand == hand && m.Role == MovementRole.Top).ToList())
            {
                circuit.Movements.Remove(movement);
            }
        }

        foreach (var footHold in circuit.Movements
                     .Where(movement =>
                         movement.WallName == wallName &&
                         movement.HoleNumber == hole.Number &&
                         movement.IsFootHold)
                     .ToList())
        {
            circuit.Movements.Remove(footHold);
        }

        var nextSequence = circuit.Movements
            .Where(movement => !movement.IsFootHold)
            .Select(m => m.Sequence)
            .DefaultIfEmpty(0)
            .Max() + 1;

        circuit.Movements.Add(new CircuitMovementDefinition
        {
            WallName = wallName,
            HoleNumber = hole.Number,
            Hand = hand,
            Role = role,
            Sequence = nextSequence
        });

        Resequence(circuit);
    }

    public void ToggleFootHold(CircuitDefinition circuit, string wallName, WallHoleDefinition hole)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        var existing = circuit.Movements.FirstOrDefault(movement =>
            movement.WallName == wallName &&
            movement.HoleNumber == hole.Number &&
            movement.IsFootHold);
        if (existing is not null)
        {
            circuit.Movements.Remove(existing);
            Resequence(circuit);
            return;
        }

        foreach (var movement in circuit.Movements
                     .Where(movement =>
                         movement.WallName == wallName &&
                         movement.HoleNumber == hole.Number)
                     .ToList())
        {
            circuit.Movements.Remove(movement);
        }

        circuit.Movements.Add(new CircuitMovementDefinition
        {
            WallName = wallName,
            HoleNumber = hole.Number,
            Hand = HandSide.Left,
            Role = MovementRole.Feet,
            Sequence = 0
        });

        Resequence(circuit);
    }

    public void RemoveHole(CircuitDefinition circuit, string wallName, WallHoleDefinition hole)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        foreach (var movement in circuit.Movements
                     .Where(m => m.WallName == wallName && m.HoleNumber == hole.Number)
                     .ToList())
        {
            circuit.Movements.Remove(movement);
        }

        Resequence(circuit);
    }

    private static void Resequence(CircuitDefinition circuit)
    {
        var footHolds = circuit.Movements
            .Where(movement => movement.IsFootHold)
            .OrderBy(movement => movement.WallName, StringComparer.Ordinal)
            .ThenBy(movement => movement.HoleNumber)
            .ToList();
        var orderedMovements = circuit.Movements
            .Where(movement => !movement.IsFootHold)
            .OrderBy(movement => GetRoleOrder(movement.Role))
            .ThenBy(movement => movement.Sequence)
            .ThenBy(movement => movement.Hand)
            .ToList();

        circuit.Movements.Clear();

        foreach (var footHold in footHolds)
        {
            circuit.Movements.Add(new CircuitMovementDefinition
            {
                WallName = footHold.WallName,
                HoleNumber = footHold.HoleNumber,
                Hand = HandSide.Left,
                Role = MovementRole.Feet,
                Sequence = 0
            });
        }

        for (var index = 0; index < orderedMovements.Count; index++)
        {
            circuit.Movements.Add(new CircuitMovementDefinition
            {
                WallName = orderedMovements[index].WallName,
                HoleNumber = orderedMovements[index].HoleNumber,
                Hand = orderedMovements[index].Hand,
                Role = orderedMovements[index].Role,
                Sequence = index + 1
            });
        }
    }

    private static int GetRoleOrder(MovementRole role)
    {
        return role switch
        {
            MovementRole.Start => 0,
            MovementRole.Normal => 1,
            MovementRole.Top => 2,
            _ => 1
        };
    }

    private static CircuitGlobalsDefinition CloneGlobals(CircuitGlobalsDefinition source)
    {
        return new CircuitGlobalsDefinition
        {
            PresetName = source.PresetName,
            Effect = source.Effect,
            DefaultBrightness = source.DefaultBrightness,
            DimmedBrightness = source.DimmedBrightness,
            RightHandColor = source.RightHandColor,
            LeftHandColor = source.LeftHandColor,
            StartColor = source.StartColor,
            TopColor = source.TopColor,
            BlinkCount = source.BlinkCount,
            BlinkPeriodMs = source.BlinkPeriodMs,
            HoldDurationMs = source.HoldDurationMs
        };
    }

    private static string NormalizeClimberProfileId(string? climberProfileId)
    {
        return string.IsNullOrWhiteSpace(climberProfileId)
            ? ClimberProfileDefinition.DefaultProfileId
            : climberProfileId.Trim();
    }

    private static void ValidateWalls(IReadOnlyList<WallDefinition> walls)
    {
        if (walls.Count == 0)
        {
            throw new InvalidOperationException("Seleziona almeno una parete per il circuito.");
        }

        var roomNames = walls
            .Select(wall => wall.RoomName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (roomNames.Count > 1)
        {
            throw new InvalidOperationException("Le pareti di un circuito devono appartenere alla stessa sala.");
        }
    }
}

