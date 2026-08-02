using RouteLab.Models;

namespace RouteLab.Services;

public interface IWallRepository
{
    Task<int> SaveAsync(WallDefinition wall, CancellationToken cancellationToken = default);

    Task SaveHoleAsync(
        WallDefinition wall,
        WallHoleDefinition hole,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WallDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
}
