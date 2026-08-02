using RouteLab.Models;

namespace RouteLab.Services;

public interface INextHoldSuggestionService
{
    NextHoldSuggestionResult SuggestNextHold(NextHoldSuggestionRequest request);
}

