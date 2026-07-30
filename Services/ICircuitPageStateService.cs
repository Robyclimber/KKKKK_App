using WallPanelPlanner.Models;
using WallPanelPlanner.ViewModels;

namespace WallPanelPlanner.Services;

public interface ICircuitPageStateService
{
    CircuitPageState Build(CircuitEditorViewModel viewModel, CircuitInteractionMode interactionMode, HandSide specialModeHand, WallDefinition? currentlySelectedWall);
}
