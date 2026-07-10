using WallPanelPlanner.Models;
using WallPanelPlanner.ViewModels;

namespace WallPanelPlanner.Services;

public interface IGymSetupEditorStateService
{
    WallEditorState BuildWallEditor(GymSetupViewModel viewModel, bool useSelectedWallValues);

    PanelEditorState BuildPanelEditor(GymSetupViewModel viewModel, bool useSelectedPanelValues);
}
