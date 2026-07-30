using RuoteLab.Models;
using RuoteLab.ViewModels;

namespace RuoteLab.Services;

public interface IGymSetupEditorStateService
{
    WallEditorState BuildWallEditor(GymSetupViewModel viewModel, bool useSelectedWallValues);

    PanelEditorState BuildPanelEditor(GymSetupViewModel viewModel, bool useSelectedPanelValues);
}
