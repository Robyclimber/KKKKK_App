using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public interface IWallRepository
{
    Task<int> SaveAsync(WallDefinition wall, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WallDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
}
