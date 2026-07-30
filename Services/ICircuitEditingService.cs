using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public interface ICircuitEditingService
{
    CircuitDefinition CreateCircuit(string? name, string? difficulty, string? inclination, WallDefinition wall, string fallbackName);

    void UpdateCircuitMetadata(CircuitDefinition circuit, string? name, string? difficulty, string? inclination);

    void ToggleMovement(CircuitDefinition circuit, string wallName, WallHoleDefinition hole, HandSide hand, MovementRole role);

    void RemoveHole(CircuitDefinition circuit, string wallName, WallHoleDefinition hole);
}
