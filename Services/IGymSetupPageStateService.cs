using WallPanelPlanner.Models;
using WallPanelPlanner.ViewModels;

namespace WallPanelPlanner.Services;

public interface IGymSetupPageStateService
{
    GymSetupPageState Build(GymSetupViewModel viewModel);
}
