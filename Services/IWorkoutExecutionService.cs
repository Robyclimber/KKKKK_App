using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IWorkoutExecutionService
{
    WorkoutExecutionState CurrentState { get; }

    event EventHandler<WorkoutExecutionState>? StateChanged;

    IReadOnlyList<WorkoutExpandedPhaseDefinition> BuildExecutionPlan(WorkoutDefinition workout);

    Task StartAsync(WorkoutDefinition workout, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
