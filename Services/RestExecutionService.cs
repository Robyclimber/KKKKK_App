using RuoteLab.Models;

namespace RuoteLab.Services;

public sealed class RestExecutionService : IRestExecutionService
{
    private readonly IEsp32ApiClient esp32ApiClient;
    private readonly IEsp32SettingsService esp32SettingsService;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? runCancellationSource;
    private RestExecutionState currentState = RestExecutionState.CreateIdle();

    public RestExecutionService(IEsp32ApiClient esp32ApiClient, IEsp32SettingsService esp32SettingsService)
    {
        this.esp32ApiClient = esp32ApiClient;
        this.esp32SettingsService = esp32SettingsService;
    }

    public RestExecutionState CurrentState => currentState;

    public event EventHandler<RestExecutionState>? StateChanged;

    public async Task StartAsync(RestStepDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateDefinition(definition);

        CancellationTokenSource? localRunCancellationSource;
        await gate.WaitAsync(cancellationToken);
        try
        {
            runCancellationSource?.Cancel();
            runCancellationSource?.Dispose();
            runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localRunCancellationSource = runCancellationSource;

            var startedAt = DateTimeOffset.UtcNow;
            SetState(new RestExecutionState
            {
                Status = RestExecutionStatus.Running,
                Definition = definition,
                RemainingSeconds = definition.DurationSeconds,
                StartedAtUtc = startedAt,
                EndsAtUtc = startedAt.AddSeconds(definition.DurationSeconds),
                StatusMessage = $"Recupero attivo sulla parete {definition.WallName}.",
                LedFeedbackMessage = "Invio feedback LED di recupero..."
            });
        }
        finally
        {
            gate.Release();
        }

        await SendRunningFeedbackAsync(definition, cancellationToken);
        _ = RunCountdownAsync(localRunCancellationSource!);
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        RestStepDefinition? definition;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != RestExecutionStatus.Running || currentState.Definition is null)
            {
                return;
            }

            runCancellationSource?.Cancel();
            definition = currentState.Definition;
            SetState(new RestExecutionState
            {
                Status = RestExecutionStatus.Paused,
                Definition = currentState.Definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = "Recupero in pausa.",
                LedFeedbackMessage = "Pausa richiesta, spegnimento LED in corso..."
            });
        }
        finally
        {
            gate.Release();
        }

        await ClearFeedbackAsync(definition, cancellationToken, "Pausa recupero: LED spenti.");
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        RestStepDefinition? definition;
        CancellationTokenSource? localRunCancellationSource;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != RestExecutionStatus.Paused || currentState.Definition is null || currentState.RemainingSeconds <= 0)
            {
                return;
            }

            definition = currentState.Definition;
            runCancellationSource?.Dispose();
            runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localRunCancellationSource = runCancellationSource;

            var startedAt = DateTimeOffset.UtcNow;
            SetState(new RestExecutionState
            {
                Status = RestExecutionStatus.Running,
                Definition = definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = startedAt,
                EndsAtUtc = startedAt.AddSeconds(currentState.RemainingSeconds),
                StatusMessage = $"Recupero ripreso sulla parete {definition.WallName}.",
                LedFeedbackMessage = "Ripresa recupero: invio feedback LED..."
            });
        }
        finally
        {
            gate.Release();
        }

        await SendRunningFeedbackAsync(definition!, cancellationToken);
        _ = RunCountdownAsync(localRunCancellationSource!);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        RestStepDefinition? definition;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status == RestExecutionStatus.Idle)
            {
                return;
            }

            runCancellationSource?.Cancel();
            definition = currentState.Definition;
            SetState(new RestExecutionState
            {
                Status = RestExecutionStatus.Cancelled,
                Definition = currentState.Definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = "Recupero terminato manualmente.",
                LedFeedbackMessage = "Termine richiesto, spegnimento LED in corso..."
            });
        }
        finally
        {
            gate.Release();
        }

        await ClearFeedbackAsync(definition, cancellationToken, "Recupero terminato: LED spenti.");
    }

    private async Task RunCountdownAsync(CancellationTokenSource localRunCancellationSource)
    {
        try
        {
            while (!localRunCancellationSource.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), localRunCancellationSource.Token);

                await gate.WaitAsync(localRunCancellationSource.Token);
                try
                {
                    if (!ReferenceEquals(runCancellationSource, localRunCancellationSource) ||
                        currentState.Status != RestExecutionStatus.Running ||
                        currentState.Definition is null)
                    {
                        return;
                    }

                    var remaining = Math.Max(0, currentState.RemainingSeconds - 1);
                    var endedAt = DateTimeOffset.UtcNow.AddSeconds(remaining);
                    SetState(new RestExecutionState
                    {
                        Status = RestExecutionStatus.Running,
                        Definition = currentState.Definition,
                        RemainingSeconds = remaining,
                        StartedAtUtc = currentState.StartedAtUtc,
                        EndsAtUtc = endedAt,
                        StatusMessage = $"Recupero in corso sulla parete {currentState.Definition.WallName}.",
                        LedFeedbackMessage = currentState.LedFeedbackMessage
                    });

                    if (remaining > 0)
                    {
                        continue;
                    }
                }
                finally
                {
                    gate.Release();
                }

                await CompleteAsync(localRunCancellationSource.Token);
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CompleteAsync(CancellationToken cancellationToken)
    {
        RestStepDefinition? definition;
        int holdSeconds;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Definition is null)
            {
                return;
            }

            definition = currentState.Definition;
            holdSeconds = Math.Max(0, definition.CompletedHoldSeconds);
            SetState(new RestExecutionState
            {
                Status = RestExecutionStatus.Completed,
                Definition = definition,
                RemainingSeconds = 0,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = DateTimeOffset.UtcNow,
                StatusMessage = "Recupero completato.",
                LedFeedbackMessage = "Invio feedback LED di fine recupero..."
            });
        }
        finally
        {
            gate.Release();
        }

        await SendCompletedFeedbackAsync(definition!, cancellationToken);

        if (holdSeconds > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(holdSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        await ClearFeedbackAsync(definition, cancellationToken, "Recupero completato: LED spenti dopo conferma verde.");
    }

    private async Task SendRunningFeedbackAsync(RestStepDefinition definition, CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.StartRestFeedbackAsync(settings, new Esp32RestFeedbackStartRequest
            {
                WallId = definition.WallId,
                WallName = definition.WallName,
                DurationSeconds = definition.DurationSeconds,
                BlinkColor = definition.BlinkColor,
                BlinkPeriodMs = definition.BlinkPeriodMs
            }, cancellationToken);

            await UpdateLedFeedbackMessageAsync(response.Success
                ? $"Recupero LED avviato su ESP32 per {definition.WallName}."
                : $"Timer attivo, ma feedback LED recupero KO - {response.ErrorCode} - {response.Message}");
        }
        catch (Exception ex)
        {
            await UpdateLedFeedbackMessageAsync($"Timer attivo, ma feedback LED recupero non disponibile - {ex.Message}");
        }
    }

    private async Task SendCompletedFeedbackAsync(RestStepDefinition definition, CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.CompleteRestFeedbackAsync(settings, new Esp32RestFeedbackCompleteRequest
            {
                WallId = definition.WallId,
                WallName = definition.WallName,
                CompletedColor = definition.CompletedColor,
                HoldSeconds = definition.CompletedHoldSeconds
            }, cancellationToken);

            await UpdateLedFeedbackMessageAsync(response.Success
                ? "Feedback LED fine recupero inviato."
                : $"Recupero completato, ma feedback LED finale KO - {response.ErrorCode} - {response.Message}");
        }
        catch (Exception ex)
        {
            await UpdateLedFeedbackMessageAsync($"Recupero completato, ma feedback LED finale non disponibile - {ex.Message}");
        }
    }

    private async Task ClearFeedbackAsync(RestStepDefinition? definition, CancellationToken cancellationToken, string successMessage)
    {
        if (definition is null)
        {
            return;
        }

        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.ClearRestFeedbackAsync(settings, cancellationToken);
            await UpdateLedFeedbackMessageAsync(response.Success
                ? successMessage
                : $"Richiesta clear LED KO - {response.ErrorCode} - {response.Message}");
        }
        catch (Exception ex)
        {
            await UpdateLedFeedbackMessageAsync($"Clear LED non disponibile - {ex.Message}");
        }
    }

    private async Task UpdateLedFeedbackMessageAsync(string message)
    {
        await gate.WaitAsync();
        try
        {
            SetState(new RestExecutionState
            {
                Status = currentState.Status,
                Definition = currentState.Definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = currentState.EndsAtUtc,
                StatusMessage = currentState.StatusMessage,
                LedFeedbackMessage = message
            });
        }
        finally
        {
            gate.Release();
        }
    }

    private void SetState(RestExecutionState state)
    {
        currentState = state;
        StateChanged?.Invoke(this, state);
    }

    private static void ValidateDefinition(RestStepDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.WallId) || string.IsNullOrWhiteSpace(definition.WallName))
        {
            throw new InvalidOperationException("Seleziona una parete valida per il recupero.");
        }

        if (definition.DurationSeconds <= 0)
        {
            throw new InvalidOperationException("La durata del recupero deve essere maggiore di zero.");
        }

        if (definition.BlinkPeriodMs < 100)
        {
            throw new InvalidOperationException("Il periodo lampeggio deve essere almeno 100 ms.");
        }

        if (definition.CompletedHoldSeconds < 0)
        {
            throw new InvalidOperationException("La conferma verde non puo' avere durata negativa.");
        }
    }
}
