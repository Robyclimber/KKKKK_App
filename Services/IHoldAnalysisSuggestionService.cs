using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public interface IHoldAnalysisSuggestionService
{
    HoldSuggestion Suggest(WallDefinition wall, WallHoleDefinition hole);
}
