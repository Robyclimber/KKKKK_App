using SQLite;
using RouteLab.Models;
using RouteLab.Persistence;
using RouteLab.Persistence.Entities;
using System.Text.Json;

namespace RouteLab.Services;

public sealed class SqliteCircuitRepository : ICircuitRepository
{
    private readonly ISqliteDatabaseFactory databaseFactory;
    private readonly IBusyIndicatorService busyIndicatorService;

    public SqliteCircuitRepository(
        ISqliteDatabaseFactory databaseFactory,
        IBusyIndicatorService busyIndicatorService)
    {
        this.databaseFactory = databaseFactory;
        this.busyIndicatorService = busyIndicatorService;
    }

    public async Task<IReadOnlyList<CircuitDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await busyIndicatorService.RunAsync("Caricamento circuiti...", async () =>
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
                    CircuitId = circuitEntity.CircuitId,
                    RoomName = string.IsNullOrWhiteSpace(circuitEntity.RoomName) ? "Sala Arrampicata" : circuitEntity.RoomName,
                    Name = circuitEntity.Name,
                    Difficulty = circuitEntity.Difficulty,
                    Inclination = circuitEntity.Inclination,
                    ClimberProfileId = string.IsNullOrWhiteSpace(circuitEntity.ClimberProfileId)
                        ? ClimberProfileDefinition.DefaultProfileId
                        : circuitEntity.ClimberProfileId,
                    SuggestNextHoldEnabled = circuitEntity.SuggestNextHoldEnabled,
                    WallName = circuitEntity.WallName,
                    Globals = new CircuitGlobalsDefinition
                    {
                        PresetName = circuitEntity.PresetName,
                        Effect = circuitEntity.Effect,
                        DefaultBrightness = circuitEntity.DefaultBrightness,
                        DimmedBrightness = circuitEntity.DimmedBrightness,
                        RightHandColor = circuitEntity.RightHandColor,
                        LeftHandColor = circuitEntity.LeftHandColor,
                        StartColor = circuitEntity.StartColor,
                        TopColor = circuitEntity.TopColor,
                        BlinkCount = circuitEntity.BlinkCount,
                        BlinkPeriodMs = circuitEntity.BlinkPeriodMs,
                        HoldDurationMs = circuitEntity.HoldDurationMs
                    }
                };

                circuit.SetWallNames(DeserializeWallNames(circuitEntity.WallNamesJson, circuitEntity.WallName));

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
        });
    }

    public async Task<int> SaveAsync(CircuitDefinition circuit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        return await busyIndicatorService.RunAsync("Salvataggio circuito...", async () =>
        {
            var connection = await databaseFactory.GetConnectionAsync();
            var entity = circuit.Id > 0
                ? await connection.Table<CircuitEntity>().Where(item => item.Id == circuit.Id).FirstOrDefaultAsync()
                : null;

            entity ??= new CircuitEntity();
            entity.CircuitId = string.IsNullOrWhiteSpace(circuit.CircuitId) ? Guid.NewGuid().ToString("N") : circuit.CircuitId;
            entity.RoomName = string.IsNullOrWhiteSpace(circuit.RoomName) ? "Sala Arrampicata" : circuit.RoomName;
            entity.Name = circuit.Name;
            var wallNames = circuit.GetWallNames();
            entity.WallName = wallNames.FirstOrDefault() ?? circuit.WallName;
            entity.WallNamesJson = JsonSerializer.Serialize(wallNames);
            entity.Difficulty = circuit.Difficulty;
            entity.Inclination = circuit.Inclination;
            entity.ClimberProfileId = string.IsNullOrWhiteSpace(circuit.ClimberProfileId)
                ? ClimberProfileDefinition.DefaultProfileId
                : circuit.ClimberProfileId;
            entity.SuggestNextHoldEnabled = circuit.SuggestNextHoldEnabled;
            entity.PresetName = circuit.Globals.PresetName;
            entity.Effect = circuit.Globals.Effect;
            entity.DefaultBrightness = circuit.Globals.DefaultBrightness;
            entity.DimmedBrightness = circuit.Globals.DimmedBrightness;
            entity.RightHandColor = circuit.Globals.RightHandColor;
            entity.LeftHandColor = circuit.Globals.LeftHandColor;
            entity.StartColor = circuit.Globals.StartColor;
            entity.TopColor = circuit.Globals.TopColor;
            entity.BlinkCount = circuit.Globals.BlinkCount;
            entity.BlinkPeriodMs = circuit.Globals.BlinkPeriodMs;
            entity.HoldDurationMs = circuit.Globals.HoldDurationMs;
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
            circuit.CircuitId = entity.CircuitId;
            circuit.SetWallNames(wallNames);
            return entity.Id;
        });
    }

    public async Task DeleteAsync(int circuitId, CancellationToken cancellationToken = default)
    {
        if (circuitId <= 0)
        {
            return;
        }

        await busyIndicatorService.RunAsync("Eliminazione circuito...", async () =>
        {
            var connection = await databaseFactory.GetConnectionAsync();
            await connection.Table<CircuitMovementEntity>().DeleteAsync(item => item.CircuitId == circuitId);
            await connection.DeleteAsync<CircuitEntity>(circuitId);
        });
    }

    private static IReadOnlyList<string> DeserializeWallNames(string? json, string? legacyWallName)
    {
        try
        {
            var names = JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();
            var normalizedNames = names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (normalizedNames.Count > 0)
            {
                return normalizedNames;
            }
        }
        catch (JsonException)
        {
            // Legacy or malformed data falls back to the historical primary wall.
        }

        return string.IsNullOrWhiteSpace(legacyWallName)
            ? Array.Empty<string>()
            : new[] { legacyWallName.Trim() };
    }
}
