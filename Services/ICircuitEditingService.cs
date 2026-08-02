using RouteLab.Models;

namespace RouteLab.Services;

public interface ICircuitEditingService
{
    CircuitDefinition CreateCircuit(string? name, string? difficulty, string? inclination, string? climberProfileId, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals, IReadOnlyList<WallDefinition> walls, string fallbackName);

    void UpdateCircuitMetadata(CircuitDefinition circuit, string? name, string? difficulty, string? inclination, string? climberProfileId, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals);

    void UpdateCircuitWalls(CircuitDefinition circuit, IReadOnlyList<WallDefinition> walls);

    void ToggleMovement(CircuitDefinition circuit, string wallName, WallHoleDefinition hole, HandSide hand, MovementRole role);

    void ToggleFootHold(CircuitDefinition circuit, string wallName, WallHoleDefinition hole);

    void RemoveHole(CircuitDefinition circuit, string wallName, WallHoleDefinition hole);
}

