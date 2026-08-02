namespace RouteLab.Models;

public sealed class WorkoutRestStepPayload
{
    public string? BlinkColor { get; init; }

    public string? CompletedColor { get; init; }

    public int? BlinkPeriodMs { get; init; }

    public int? CompletedHoldSeconds { get; init; }
}

public sealed class WorkoutResistanceStepPayload
{
    public string ActiveMode { get; init; } = "steady";

    public string? ActiveColor { get; init; }

    public string? CompletedColor { get; init; }

    public int? BlinkPeriodMs { get; init; }

    public int? CompletedHoldSeconds { get; init; }
}

public sealed class WorkoutHangStepPayload
{
    public List<int> TargetHoleNumbers { get; init; } = new();

    public string? ActiveColor { get; init; }

    public string? CompletedColor { get; init; }

    public int? CompletedHoldSeconds { get; init; }
}

public sealed class WorkoutCircuitStepPayload
{
    public string CircuitId { get; init; } = string.Empty;

    public string CircuitName { get; init; } = string.Empty;

    public WorkoutCircuitMode Mode { get; init; } = WorkoutCircuitMode.Start;
}

