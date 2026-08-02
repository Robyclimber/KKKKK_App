using RouteLab.Models;

namespace RouteLab.Services;

public interface IRoomRepository
{
    Task<IReadOnlyList<RoomDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<int> SaveAsync(RoomDefinition room, CancellationToken cancellationToken = default);
}

