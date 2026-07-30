using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public sealed class CircuitEditingService : ICircuitEditingService
{
    public CircuitDefinition CreateCircuit(string? name, string? difficulty, string? inclination, WallDefinition wall, string fallbackName)
    {
        ArgumentNullException.ThrowIfNull(wall);

        return new CircuitDefinition
        {
            Name = string.IsNullOrWhiteSpace(name) ? fallbackName : name.Trim(),
            Difficulty = difficulty?.Trim() ?? string.Empty,
            Inclination = inclination?.Trim() ?? string.Empty,
            RoomName = wall.RoomName,
            WallName = wall.Name
        };
    }

    public void UpdateCircuitMetadata(CircuitDefinition circuit, string? name, string? difficulty, string? inclination)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        circuit.Name = string.IsNullOrWhiteSpace(name) ? circuit.Name : name.Trim();
        circuit.Difficulty = difficulty?.Trim() ?? string.Empty;
        circuit.Inclination = inclination?.Trim() ?? string.Empty;
    }

    public void ToggleMovement(CircuitDefinition circuit, string wallName, WallHoleDefinition hole, HandSide hand, MovementRole role)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        var existing = circuit.Movements
            .FirstOrDefault(m => m.WallName == wallName && m.HoleNumber == hole.Number && m.Hand == hand);

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

        var nextSequence = circuit.Movements
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
        var orderedMovements = circuit.Movements
            .OrderBy(movement => GetRoleOrder(movement.Role))
            .ThenBy(movement => movement.Sequence)
            .ThenBy(movement => movement.Hand)
            .ToList();

        circuit.Movements.Clear();

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
}
