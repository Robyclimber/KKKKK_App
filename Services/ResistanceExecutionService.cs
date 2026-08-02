using RouteLab.Models;

namespace RouteLab.Services;

public sealed class ResistanceExecutionService : IResistanceExecutionService
{
    private readonly IEsp32ApiClient esp32ApiClient;
    private readonly IEsp32SettingsService esp32SettingsService;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? runCancellationSource;
    private ResistanceExecutionState currentState = ResistanceExecutionState.CreateIdle();

    public ResistanceExecutionService(IEsp32ApiClient esp32ApiClient, IEsp32SettingsService esp32SettingsService)
    {
        this.esp32ApiClient = esp32ApiClient;
        this.esp32SettingsService = esp32SettingsService;
    }

    public ResistanceExecutionState CurrentState => currentState;

    public event EventHandler<ResistanceExecutionState>? StateChanged;

    public async Task StartAsync(ResistanceStepDefinition definition, CancellationToken cancellationToken = default)
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
            SetState(new ResistanceExecutionState
            {
                Status = ResistanceExecutionStatus.Running,
                Definition = definition,
                RemainingSeconds = definition.DurationSeconds,
                StartedAtUtc = startedAt,
                EndsAtUtc = startedAt.AddSeconds(definition.DurationSeconds),
                StatusMessage = $"Resistenza attiva sulla parete {definition.WallName}.",
                LedFeedbackMessage = "Invio feedback LED di resistenza..."
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
        ResistanceStepDefinition? definition;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != ResistanceExecutionStatus.Running || currentState.Definition is null)
            {
                return;
            }

            runCancellationSource?.Cancel();
            definition = currentState.Definition;
            SetState(new ResistanceExecutionState
            {
                Status = ResistanceExecutionStatus.Paused,
                Definition = currentState.Definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = "Resistenza in pausa.",
                LedFeedbackMessage = "Pausa richiesta, spegnimento LED in corso..."
            });
        }
        finally
        {
            gate.Release();
        }

        await ClearFeedbackAsync(definition, cancellationToken, "Pausa resistenza: LED spenti.");
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        ResistanceStepDefinition? definition;
        CancellationTokenSource? localRunCancellationSource;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != ResistanceExecutionStatus.Paused || currentState.Definition is null || currentState.RemainingSeconds <= 0)
            {
                return;
            }

            definition = currentState.Definition;
            runCancellationSource?.Dispose();
            runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localRunCancellationSource = runCancellationSource;

            var startedAt = DateTimeOffset.UtcNow;
            SetState(new ResistanceExecutionState
            {
                Status = ResistanceExecutionStatus.Running,
                Definition = definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = startedAt,
                EndsAtUtc = startedAt.AddSeconds(currentState.RemainingSeconds),
                StatusMessage = $"Resistenza ripresa sulla parete {definition.WallName}.",
                LedFeedbackMessage = "Ripresa resistenza: invio feedback LED..."
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
        ResistanceStepDefinition? definition;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status == ResistanceExecutionStatus.Idle)
            {
                return;
            }

            runCancellationSource?.Cancel();
            definition = currentState.Definition;
            SetState(new ResistanceExecutionState
            {
                Status = ResistanceExecutionStatus.Cancelled,
                Definition = currentState.Definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = "Resistenza terminata manualmente.",
                LedFeedbackMessage = "Termine richiesto, spegnimento LED in corso..."
            });
        }
        finally
        {
            gate.Release();
        }

        await ClearFeedbackAsync(definition, cancellationToken, "Resistenza terminata: LED spenti.");
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
                        currentState.Status != ResistanceExecutionStatus.Running ||
                        currentState.Definition is null)
                    {
                        return;
                    }

                    var remaining = Math.Max(0, currentState.RemainingSeconds - 1);
                    var endedAt = DateTimeOffset.UtcNow.AddSeconds(remaining);
                    SetState(new ResistanceExecutionState
                    {
                        Status = ResistanceExecutionStatus.Running,
                        Definition = currentState.Definition,
                        RemainingSeconds = remaining,
                        StartedAtUtc = currentState.StartedAtUtc,
                        EndsAtUtc = endedAt,
                        StatusMessage = $"Resistenza in corso sulla parete {currentState.Definition.WallName}.",
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
        ResistanceStepDefinition? definition;
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
            SetState(new ResistanceExecutionState
            {
                Status = ResistanceExecutionStatus.Completed,
                Definition = definition,
                RemainingSeconds = 0,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = DateTimeOffset.UtcNow,
                StatusMessage = "Resistenza completata.",
                LedFeedbackMessage = "Invio feedback LED di fine resistenza..."
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

        await ClearFeedbackAsync(definition, cancellationToken, "Resistenza completata: LED spenti dopo conferma verde.");
    }

    private async Task SendRunningFeedbackAsync(ResistanceStepDefinition definition, CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.StartResistanceFeedbackAsync(settings, new Esp32ResistanceFeedbackStartRequest
            {
                WallId = definition.WallId,
                WallName = definition.WallName,
                DurationSeconds = definition.DurationSeconds,
                ActiveColor = definition.ActiveColor,
                ActiveMode = definition.ActiveMode,
                BlinkPeriodMs = definition.BlinkPeriodMs
            }, cancellationToken);

            await UpdateLedFeedbackMessageAsync(response.Success
                ? $"Resistenza LED avviata su RouteLab Hub per {definition.WallName}."
                : $"Timer attivo, ma feedback LED resistenza KO - {response.ErrorCode} - {response.Message}");
        }
        catch (Exception ex)
        {
            await UpdateLedFeedbackMessageAsync($"Timer attivo, ma feedback LED resistenza non disponibile - {ex.Message}");
        }
    }

    private async Task SendCompletedFeedbackAsync(ResistanceStepDefinition definition, CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.CompleteResistanceFeedbackAsync(settings, new Esp32ResistanceFeedbackCompleteRequest
            {
                WallId = definition.WallId,
                WallName = definition.WallName,
                CompletedColor = definition.CompletedColor,
                HoldSeconds = definition.CompletedHoldSeconds
            }, cancellationToken);

            await UpdateLedFeedbackMessageAsync(response.Success
                ? "Feedback LED fine resistenza inviato."
                : $"Resistenza completata, ma feedback LED finale KO - {response.ErrorCode} - {response.Message}");
        }
        catch (Exception ex)
        {
            await UpdateLedFeedbackMessageAsync($"Resistenza completata, ma feedback LED finale non disponibile - {ex.Message}");
        }
    }

    private async Task ClearFeedbackAsync(ResistanceStepDefinition? definition, CancellationToken cancellationToken, string successMessage)
    {
        if (definition is null)
        {
            return;
        }

        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.ClearResistanceFeedbackAsync(settings, cancellationToken);
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
            SetState(new ResistanceExecutionState
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

    private void SetState(ResistanceExecutionState state)
    {
        currentState = state;
        StateChanged?.Invoke(this, state);
    }

    private static void ValidateDefinition(ResistanceStepDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.WallId) || string.IsNullOrWhiteSpace(definition.WallName))
        {
            throw new InvalidOperationException("Seleziona una parete valida per la resistenza.");
        }

        if (definition.DurationSeconds <= 0)
        {
            throw new InvalidOperationException("La durata della resistenza deve essere maggiore di zero.");
        }

        if (definition.BlinkPeriodMs < 100)
        {
            throw new InvalidOperationException("Il periodo feedback deve essere almeno 100 ms.");
        }
    }
}



