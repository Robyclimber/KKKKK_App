using RouteLab.Models;

namespace RouteLab.Services;

public interface IResistanceExecutionService
{
    ResistanceExecutionState CurrentState { get; }

    event EventHandler<ResistanceExecutionState>? StateChanged;

    Task StartAsync(ResistanceStepDefinition definition, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

