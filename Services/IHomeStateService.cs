using RouteLab.Models;

namespace RouteLab.Services;

public interface IHomeStateService
{
    Task<HomeStateSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

