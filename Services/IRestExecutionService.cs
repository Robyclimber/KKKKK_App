using RouteLab.Models;

namespace RouteLab.Services;

public interface IRestExecutionService
{
    RestExecutionState CurrentState { get; }

    event EventHandler<RestExecutionState>? StateChanged;

    Task StartAsync(RestStepDefinition definition, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

