using RuoteLab.Models;

namespace RuoteLab.Services;

public interface ICircuitEditingService
{
    CircuitDefinition CreateCircuit(string? name, string? difficulty, string? inclination, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals, WallDefinition wall, string fallbackName);

    void UpdateCircuitMetadata(CircuitDefinition circuit, string? name, string? difficulty, string? inclination, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals);

    void ToggleMovement(CircuitDefinition circuit, string wallName, WallHoleDefinition hole, HandSide hand, MovementRole role);

    void RemoveHole(CircuitDefinition circuit, string wallName, WallHoleDefinition hole);
}
