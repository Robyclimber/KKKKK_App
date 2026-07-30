namespace RuoteLab.Models;

public sealed class WorkoutExecutionState
{
    public WorkoutExecutionStatus Status { get; init; } = WorkoutExecutionStatus.Idle;

    public string WorkoutId { get; init; } = string.Empty;

    public int CurrentStepIndex { get; init; } = -1;

    public int CurrentPlanIndex { get; init; } = -1;

    public int CurrentRepetition { get; init; }

    public WorkoutRuntimePhase CurrentPhase { get; init; } = WorkoutRuntimePhase.InitialRest;

    public string CurrentStepName { get; init; } = string.Empty;

    public WorkoutStepType? CurrentStepType { get; init; }

    public int RemainingSeconds { get; init; }

    public int TotalSteps { get; init; }

    public int TotalPhases { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? EndsAtUtc { get; init; }

    public string StatusMessage { get; init; } = "Allenamento pronto.";
}
