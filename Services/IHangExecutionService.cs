using RouteLab.Models;

namespace RouteLab.Services;

public interface IHangExecutionService
{
    HangExecutionState CurrentState { get; }

    event EventHandler<HangExecutionState>? StateChanged;

    Task StartAsync(HangStepDefinition definition, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

