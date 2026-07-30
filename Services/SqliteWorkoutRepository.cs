using System.Text.Json;
using System.Text.Json.Serialization;
using RuoteLab.Models;
using RuoteLab.Persistence;
using RuoteLab.Persistence.Entities;

namespace RuoteLab.Services;

public sealed class SqliteWorkoutRepository : IWorkoutRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ISqliteDatabaseFactory databaseFactory;

    public SqliteWorkoutRepository(ISqliteDatabaseFactory databaseFactory)
    {
        this.databaseFactory = databaseFactory;
    }

    public async Task<IReadOnlyList<WorkoutDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await databaseFactory.GetConnectionAsync();
        var workoutEntities = await connection.Table<WorkoutEntity>()
            .OrderByDescending(entity => entity.UpdatedAtUtcTicks)
            .ToListAsync();
        var stepEntities = await connection.Table<WorkoutStepEntity>().ToListAsync();

        var result = new List<WorkoutDefinition>(workoutEntities.Count);
        foreach (var workoutEntity in workoutEntities)
        {
            var workout = new WorkoutDefinition
            {
                WorkoutId = workoutEntity.WorkoutId,
                Name = workoutEntity.Name,
                Description = workoutEntity.Description,
                RoomName = workoutEntity.RoomName,
                WallId = workoutEntity.WallId,
                WallName = workoutEntity.WallName,
                Steps = stepEntities
                    .Where(entity => entity.WorkoutEntityId == workoutEntity.Id)
                    .OrderBy(entity => entity.Sequence)
                    .Select(MapStep)
                    .ToList()
            };

            result.Add(workout);
        }

        return result;
    }

    public async Task<int> SaveAsync(WorkoutDefinition workout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workout);

        var connection = await databaseFactory.GetConnectionAsync();
        var entity = await connection.Table<WorkoutEntity>()
            .Where(item => item.WorkoutId == workout.WorkoutId)
            .FirstOrDefaultAsync();

        entity ??= new WorkoutEntity();
        entity.WorkoutId = string.IsNullOrWhiteSpace(workout.WorkoutId) ? Guid.NewGuid().ToString("N") : workout.WorkoutId;
        entity.RoomName = workout.RoomName;
        entity.WallId = workout.WallId;
        entity.WallName = workout.WallName;
        entity.Name = workout.Name;
        entity.Description = workout.Description;
        entity.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;

        if (entity.Id == 0)
        {
            await connection.InsertAsync(entity);
        }
        else
        {
            await connection.UpdateAsync(entity);
            await connection.Table<WorkoutStepEntity>().DeleteAsync(item => item.WorkoutEntityId == entity.Id);
        }

        for (var index = 0; index < workout.Steps.Count; index++)
        {
            var step = workout.Steps[index];
            await connection.InsertAsync(new WorkoutStepEntity
            {
                WorkoutEntityId = entity.Id,
                StepId = step.StepId,
                StepType = (int)step.StepType,
                Name = step.Name,
                WorkSeconds = step.WorkSeconds,
                InitialRestSeconds = step.InitialRestSeconds,
                FinalRestSeconds = step.FinalRestSeconds,
                Repetitions = step.Repetitions,
                Sequence = index,
                PayloadJson = SerializePayload(step)
            });
        }

        return entity.Id;
    }

    public async Task DeleteAsync(string workoutId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workoutId))
        {
            return;
        }

        var connection = await databaseFactory.GetConnectionAsync();
        var entity = await connection.Table<WorkoutEntity>()
            .Where(item => item.WorkoutId == workoutId)
            .FirstOrDefaultAsync();

        if (entity is null)
        {
            return;
        }

        await connection.Table<WorkoutStepEntity>().DeleteAsync(item => item.WorkoutEntityId == entity.Id);
        await connection.DeleteAsync<WorkoutEntity>(entity.Id);
    }

    private static WorkoutStepDefinition MapStep(WorkoutStepEntity entity)
    {
        var stepType = (WorkoutStepType)entity.StepType;
        return new WorkoutStepDefinition
        {
            StepId = entity.StepId,
            StepType = stepType,
            Name = entity.Name,
            WorkSeconds = entity.WorkSeconds,
            InitialRestSeconds = entity.InitialRestSeconds,
            FinalRestSeconds = entity.FinalRestSeconds,
            Repetitions = entity.Repetitions,
            RestPayload = stepType == WorkoutStepType.Rest
                ? DeserializePayload<WorkoutRestStepPayload>(entity.PayloadJson)
                : null,
            ResistancePayload = stepType == WorkoutStepType.Resistance
                ? DeserializePayload<WorkoutResistanceStepPayload>(entity.PayloadJson)
                : null,
            HangPayload = stepType == WorkoutStepType.Hang
                ? DeserializePayload<WorkoutHangStepPayload>(entity.PayloadJson)
                : null,
            CircuitPayload = stepType == WorkoutStepType.Circuit
                ? DeserializePayload<WorkoutCircuitStepPayload>(entity.PayloadJson)
                : null
        };
    }

    private static string SerializePayload(WorkoutStepDefinition step)
    {
        return step.StepType switch
        {
            WorkoutStepType.Rest => JsonSerializer.Serialize(step.RestPayload, JsonOptions),
            WorkoutStepType.Resistance => JsonSerializer.Serialize(step.ResistancePayload, JsonOptions),
            WorkoutStepType.Hang => JsonSerializer.Serialize(step.HangPayload, JsonOptions),
            WorkoutStepType.Circuit => JsonSerializer.Serialize(step.CircuitPayload, JsonOptions),
            _ => string.Empty
        };
    }

    private static TPayload? DeserializePayload<TPayload>(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return default;
        }

        return JsonSerializer.Deserialize<TPayload>(payloadJson, JsonOptions);
    }
}
