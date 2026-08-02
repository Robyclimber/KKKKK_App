namespace RouteLab.Models;

public enum HangExecutionStatus
{
    Idle,
    Running,
    Paused,
    Completed,
    Cancelled
}

public sealed class HangExecutionState
{
    public HangExecutionStatus Status { get; init; } = HangExecutionStatus.Idle;

    public HangStepDefinition? Definition { get; init; }

    public int RemainingSeconds { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? EndsAtUtc { get; init; }

    public string StatusMessage { get; init; } = "Sospensione pronta.";

    public string LedFeedbackMessage { get; init; } = "Feedback LED non ancora inviato.";

    public static HangExecutionState CreateIdle()
    {
        return new HangExecutionState
        {
            Status = HangExecutionStatus.Idle,
            RemainingSeconds = 0,
            StatusMessage = "Sospensione pronta.",
            LedFeedbackMessage = "Feedback LED non ancora inviato."
        };
    }
}

