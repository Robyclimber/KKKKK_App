namespace RuoteLab.Models;

public enum ResistanceExecutionStatus
{
    Idle,
    Running,
    Paused,
    Completed,
    Cancelled
}

public sealed class ResistanceExecutionState
{
    public ResistanceExecutionStatus Status { get; init; } = ResistanceExecutionStatus.Idle;

    public ResistanceStepDefinition? Definition { get; init; }

    public int RemainingSeconds { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? EndsAtUtc { get; init; }

    public string StatusMessage { get; init; } = "Resistenza pronta.";

    public string LedFeedbackMessage { get; init; } = "Feedback LED non ancora inviato.";

    public static ResistanceExecutionState CreateIdle()
    {
        return new ResistanceExecutionState
        {
            Status = ResistanceExecutionStatus.Idle,
            RemainingSeconds = 0,
            StatusMessage = "Resistenza pronta.",
            LedFeedbackMessage = "Feedback LED non ancora inviato."
        };
    }
}
