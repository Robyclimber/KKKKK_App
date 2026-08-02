using RouteLab.Models;
using RouteLab.ViewModels;

namespace RouteLab.Services;

public interface IGymSetupEditorStateService
{
    WallEditorState BuildWallEditor(GymSetupViewModel viewModel, bool useSelectedWallValues);

    PanelEditorState BuildPanelEditor(GymSetupViewModel viewModel, bool useSelectedPanelValues);
}

