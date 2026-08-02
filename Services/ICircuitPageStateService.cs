using RouteLab.Models;
using RouteLab.ViewModels;

namespace RouteLab.Services;

public interface ICircuitPageStateService
{
    CircuitPageState Build(CircuitEditorViewModel viewModel, CircuitInteractionMode interactionMode, HandSide specialModeHand, WallDefinition? currentlySelectedWall);
}

