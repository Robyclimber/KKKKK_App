using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public interface IHomeStateService
{
    Task<HomeStateSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
