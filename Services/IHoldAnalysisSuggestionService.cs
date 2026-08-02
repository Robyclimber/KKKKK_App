using RouteLab.Models;

namespace RouteLab.Services;

public interface IHoldAnalysisSuggestionService
{
    HoldSuggestion Suggest(WallDefinition wall, WallHoleDefinition hole);
}

