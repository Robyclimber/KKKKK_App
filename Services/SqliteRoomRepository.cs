using SQLite;
using WallPanelPlanner.Models;
using WallPanelPlanner.Persistence;
using WallPanelPlanner.Persistence.Entities;

namespace WallPanelPlanner.Services;

public sealed class SqliteRoomRepository : IRoomRepository
{
    private readonly ISqliteDatabaseFactory databaseFactory;

    public SqliteRoomRepository(ISqliteDatabaseFactory databaseFactory)
    {
        this.databaseFactory = databaseFactory;
    }

    public async Task<IReadOnlyList<RoomDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await databaseFactory.GetConnectionAsync();
        var rooms = await connection.Table<RoomEntity>()
            .OrderBy(entity => entity.Name)
            .ToListAsync();

        return rooms
            .Select(entity => new RoomDefinition
            {
                Id = entity.Id,
                Name = entity.Name
            })
            .ToList();
    }

    public async Task<int> SaveAsync(RoomDefinition room, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(room);

        var connection = await databaseFactory.GetConnectionAsync();
        var normalizedName = room.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Inserisci un nome sala valido.");
        }

        var existing = await connection.Table<RoomEntity>()
            .Where(entity => entity.Name == normalizedName)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            room.Id = existing.Id;
            return existing.Id;
        }

        var entity = new RoomEntity
        {
            Name = normalizedName
        };

        await connection.InsertAsync(entity);
        room.Id = entity.Id;
        return entity.Id;
    }
}
