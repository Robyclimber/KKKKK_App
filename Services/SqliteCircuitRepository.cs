using SQLite;
using WallPanelPlanner.Models;
using WallPanelPlanner.Persistence;
using WallPanelPlanner.Persistence.Entities;

namespace WallPanelPlanner.Services;

public sealed class SqliteCircuitRepository : ICircuitRepository
{
    private readonly ISqliteDatabaseFactory databaseFactory;

    public SqliteCircuitRepository(ISqliteDatabaseFactory databaseFactory)
    {
        this.databaseFactory = databaseFactory;
    }

    public async Task<IReadOnlyList<CircuitDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await databaseFactory.GetConnectionAsync();
        var circuitEntities = await connection.Table<CircuitEntity>()
            .OrderBy(entity => entity.Name)
            .ToListAsync();
        var movementEntities = await connection.Table<CircuitMovementEntity>().ToListAsync();

        var result = new List<CircuitDefinition>(circuitEntities.Count);
        foreach (var circuitEntity in circuitEntities)
        {
            var circuit = new CircuitDefinition
            {
                Id = circuitEntity.Id,
                RoomName = string.IsNullOrWhiteSpace(circuitEntity.RoomName) ? "Sala Arrampicata" : circuitEntity.RoomName,
                Name = circuitEntity.Name,
                Difficulty = circuitEntity.Difficulty,
                Inclination = circuitEntity.Inclination,
                WallName = circuitEntity.WallName
            };

            foreach (var movementEntity in movementEntities
                         .Where(entity => entity.CircuitId == circuitEntity.Id)
                         .OrderBy(entity => entity.Hand)
                         .ThenBy(entity => entity.Sequence))
            {
                circuit.Movements.Add(new CircuitMovementDefinition
                {
                    WallName = movementEntity.WallName,
                    HoleNumber = movementEntity.HoleNumber,
                    Hand = (HandSide)movementEntity.Hand,
                    Role = (MovementRole)movementEntity.Role,
                    Sequence = movementEntity.Sequence
                });
            }

            result.Add(circuit);
        }

        return result;
    }

    public async Task<int> SaveAsync(CircuitDefinition circuit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        var connection = await databaseFactory.GetConnectionAsync();
        var entity = circuit.Id > 0
            ? await connection.Table<CircuitEntity>().Where(item => item.Id == circuit.Id).FirstOrDefaultAsync()
            : null;

        entity ??= new CircuitEntity();
        entity.RoomName = string.IsNullOrWhiteSpace(circuit.RoomName) ? "Sala Arrampicata" : circuit.RoomName;
        entity.Name = circuit.Name;
        entity.WallName = circuit.WallName;
        entity.Difficulty = circuit.Difficulty;
        entity.Inclination = circuit.Inclination;
        entity.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;

        if (entity.Id == 0)
        {
            await connection.InsertAsync(entity);
        }
        else
        {
            await connection.UpdateAsync(entity);
            await connection.Table<CircuitMovementEntity>().DeleteAsync(item => item.CircuitId == entity.Id);
        }

        foreach (var movement in circuit.Movements)
        {
            await connection.InsertAsync(new CircuitMovementEntity
            {
                CircuitId = entity.Id,
                WallName = movement.WallName,
                HoleNumber = movement.HoleNumber,
                Hand = (int)movement.Hand,
                Role = (int)movement.Role,
                Sequence = movement.Sequence
            });
        }

        circuit.Id = entity.Id;
        return entity.Id;
    }

    public async Task DeleteAsync(int circuitId, CancellationToken cancellationToken = default)
    {
        if (circuitId <= 0)
        {
            return;
        }

        var connection = await databaseFactory.GetConnectionAsync();
        await connection.Table<CircuitMovementEntity>().DeleteAsync(item => item.CircuitId == circuitId);
        await connection.DeleteAsync<CircuitEntity>(circuitId);
    }
}
