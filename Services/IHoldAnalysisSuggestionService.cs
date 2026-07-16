using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IHoldAnalysisSuggestionService
{
    HoldSuggestion Suggest(WallDefinition wall, WallHoleDefinition hole);
}
