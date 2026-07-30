using RuoteLab.Models;
using RuoteLab.ViewModels;

namespace RuoteLab.Services;

public interface IGymSetupPageStateService
{
    GymSetupPageState Build(GymSetupViewModel viewModel);
}
