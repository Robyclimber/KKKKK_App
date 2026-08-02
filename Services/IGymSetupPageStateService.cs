using RouteLab.Models;
using RouteLab.ViewModels;

namespace RouteLab.Services;

public interface IGymSetupPageStateService
{
    GymSetupPageState Build(GymSetupViewModel viewModel);
}

