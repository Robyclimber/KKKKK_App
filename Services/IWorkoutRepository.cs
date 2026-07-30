using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IWorkoutRepository
{
    Task<IReadOnlyList<WorkoutDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<int> SaveAsync(WorkoutDefinition workout, CancellationToken cancellationToken = default);

    Task DeleteAsync(string workoutId, CancellationToken cancellationToken = default);
}
