namespace RuoteLab.Models;

public sealed class WorkoutExpandedPhaseDefinition
{
    public int PlanIndex { get; init; }

    public int StepIndex { get; init; }

    public string StepId { get; init; } = string.Empty;

    public string StepName { get; init; } = string.Empty;

    public WorkoutStepType StepType { get; init; }

    public WorkoutRuntimePhase Phase { get; init; }

    public int Repetition { get; init; }

    public int TotalRepetitions { get; init; }

    public int DurationSeconds { get; init; }
}
