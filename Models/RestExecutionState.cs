namespace RuoteLab.Models;

public enum RestExecutionStatus
{
    Idle,
    Running,
    Paused,
    Completed,
    Cancelled
}

public sealed class RestExecutionState
{
    public RestExecutionStatus Status { get; init; } = RestExecutionStatus.Idle;

    public RestStepDefinition? Definition { get; init; }

    public int RemainingSeconds { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? EndsAtUtc { get; init; }

    public string StatusMessage { get; init; } = "Recupero pronto.";

    public string LedFeedbackMessage { get; init; } = "Feedback LED non ancora inviato.";

    public static RestExecutionState CreateIdle()
    {
        return new RestExecutionState
        {
            Status = RestExecutionStatus.Idle,
            RemainingSeconds = 0,
            StatusMessage = "Recupero pronto.",
            LedFeedbackMessage = "Feedback LED non ancora inviato."
        };
    }
}
