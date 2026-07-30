using RuoteLab.Models;

namespace RuoteLab.Services;

public interface INextHoldSuggestionService
{
    NextHoldSuggestionResult SuggestNextHold(NextHoldSuggestionRequest request);
}
