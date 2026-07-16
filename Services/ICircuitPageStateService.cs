using RuoteLab.Models;
using RuoteLab.ViewModels;

namespace RuoteLab.Services;

public interface ICircuitPageStateService
{
    CircuitPageState Build(CircuitEditorViewModel viewModel, CircuitInteractionMode interactionMode, HandSide specialModeHand, WallDefinition? currentlySelectedWall);
}
