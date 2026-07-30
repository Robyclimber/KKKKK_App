using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IHomeStateService
{
    Task<HomeStateSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
