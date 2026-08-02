namespace RouteLab.Models;

public sealed class WorkoutStepDefinition
{
    public string StepId { get; init; } = Guid.NewGuid().ToString("N");

    public WorkoutStepType StepType { get; init; }

    public string Name { get; init; } = string.Empty;

    public int WorkSeconds { get; init; }

    public int InitialRestSeconds { get; init; }

    public int FinalRestSeconds { get; init; }

    public int Repetitions { get; init; } = 1;

    public WorkoutRestStepPayload? RestPayload { get; init; }

    public WorkoutResistanceStepPayload? ResistancePayload { get; init; }

    public WorkoutHangStepPayload? HangPayload { get; init; }

    public WorkoutCircuitStepPayload? CircuitPayload { get; init; }
}

