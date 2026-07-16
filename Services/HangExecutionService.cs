using RuoteLab.Models;

namespace RuoteLab.Services;

public sealed class HangExecutionService : IHangExecutionService
{
    private readonly IEsp32ApiClient esp32ApiClient;
    private readonly IEsp32SettingsService esp32SettingsService;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? runCancellationSource;
    private HangExecutionState currentState = HangExecutionState.CreateIdle();

    public HangExecutionService(IEsp32ApiClient esp32ApiClient, IEsp32SettingsService esp32SettingsService)
    {
        this.esp32ApiClient = esp32ApiClient;
        this.esp32SettingsService = esp32SettingsService;
    }

    public HangExecutionState CurrentState => currentState;

    public event EventHandler<HangExecutionState>? StateChanged;

    public async Task StartAsync(HangStepDefinition definition, CancellationToken cancellationToken = default)
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
            SetState(new HangExecutionState
            {
                Status = HangExecutionStatus.Running,
                Definition = definition,
                RemainingSeconds = definition.DurationSeconds,
                StartedAtUtc = startedAt,
                EndsAtUtc = startedAt.AddSeconds(definition.DurationSeconds),
                StatusMessage = $"Sospensione attiva su {BuildTargetsText(definition.TargetHoleNumbers)}.",
                LedFeedbackMessage = "Invio feedback LED di sospensione..."
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
        HangStepDefinition? definition;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != HangExecutionStatus.Running || currentState.Definition is null)
            {
                return;
            }

            runCancellationSource?.Cancel();
            definition = currentState.Definition;
            SetState(new HangExecutionState
            {
                Status = HangExecutionStatus.Paused,
                Definition = definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = "Sospensione in pausa.",
                LedFeedbackMessage = "Pausa richiesta, spegnimento LED in corso..."
            });
        }
        finally
        {
            gate.Release();
        }

        await ClearFeedbackAsync(definition, cancellationToken, "Pausa sospensione: LED spenti.");
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        HangStepDefinition? definition;
        CancellationTokenSource? localRunCancellationSource;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != HangExecutionStatus.Paused || currentState.Definition is null || currentState.RemainingSeconds <= 0)
            {
                return;
            }

            definition = currentState.Definition;
            runCancellationSource?.Dispose();
            runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localRunCancellationSource = runCancellationSource;

            var startedAt = DateTimeOffset.UtcNow;
            SetState(new HangExecutionState
            {
                Status = HangExecutionStatus.Running,
                Definition = definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = startedAt,
                EndsAtUtc = startedAt.AddSeconds(currentState.RemainingSeconds),
                StatusMessage = $"Sospensione ripresa su {BuildTargetsText(definition.TargetHoleNumbers)}.",
                LedFeedbackMessage = "Ripresa sospensione: invio feedback LED..."
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
        HangStepDefinition? definition;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status == HangExecutionStatus.Idle)
            {
                return;
            }

            runCancellationSource?.Cancel();
            definition = currentState.Definition;
            SetState(new HangExecutionState
            {
                Status = HangExecutionStatus.Cancelled,
                Definition = definition,
                RemainingSeconds = currentState.RemainingSeconds,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = "Sospensione terminata manualmente.",
                LedFeedbackMessage = "Termine richiesto, spegnimento LED in corso..."
            });
        }
        finally
        {
            gate.Release();
        }

        await ClearFeedbackAsync(definition, cancellationToken, "Sospensione terminata: LED spenti.");
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
                        currentState.Status != HangExecutionStatus.Running ||
                        currentState.Definition is null)
                    {
                        return;
                    }

                    var remaining = Math.Max(0, currentState.RemainingSeconds - 1);
                    var endedAt = DateTimeOffset.UtcNow.AddSeconds(remaining);
                    SetState(new HangExecutionState
                    {
                        Status = HangExecutionStatus.Running,
                        Definition = currentState.Definition,
                        RemainingSeconds = remaining,
                        StartedAtUtc = currentState.StartedAtUtc,
                        EndsAtUtc = endedAt,
                        StatusMessage = $"Sospensione in corso su {BuildTargetsText(currentState.Definition.TargetHoleNumbers)}.",
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
        HangStepDefinition? definition;
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
            SetState(new HangExecutionState
            {
                Status = HangExecutionStatus.Completed,
                Definition = definition,
                RemainingSeconds = 0,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = DateTimeOffset.UtcNow,
                StatusMessage = "Sospensione completata.",
                LedFeedbackMessage = "Invio feedback LED di fine sospensione..."
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

        await ClearFeedbackAsync(definition, cancellationToken, "Sospensione completata: LED spenti dopo conferma verde.");
    }

    private async Task SendRunningFeedbackAsync(HangStepDefinition definition, CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.StartHangFeedbackAsync(settings, new Esp32HangFeedbackStartRequest
            {
                WallId = definition.WallId,
                WallName = definition.WallName,
                DurationSeconds = definition.DurationSeconds,
                TargetHoleNumbers = definition.TargetHoleNumbers.ToList(),
                ActiveColor = definition.ActiveColor
            }, cancellationToken);

            await UpdateLedFeedbackMessageAsync(response.Success
                ? $"Sospensione LED avviata su ESP32 per {definition.WallName}."
                : $"Timer attivo, ma feedback LED sospensione KO - {response.ErrorCode} - {response.Message}");
        }
        catch (Exception ex)
        {
            await UpdateLedFeedbackMessageAsync($"Timer attivo, ma feedback LED sospensione non disponibile - {ex.Message}");
        }
    }

    private async Task SendCompletedFeedbackAsync(HangStepDefinition definition, CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.CompleteHangFeedbackAsync(settings, new Esp32HangFeedbackCompleteRequest
            {
                WallId = definition.WallId,
                WallName = definition.WallName,
                TargetHoleNumbers = definition.TargetHoleNumbers.ToList(),
                CompletedColor = definition.CompletedColor,
                HoldSeconds = definition.CompletedHoldSeconds
            }, cancellationToken);

            await UpdateLedFeedbackMessageAsync(response.Success
                ? "Feedback LED fine sospensione inviato."
                : $"Sospensione completata, ma feedback LED finale KO - {response.ErrorCode} - {response.Message}");
        }
        catch (Exception ex)
        {
            await UpdateLedFeedbackMessageAsync($"Sospensione completata, ma feedback LED finale non disponibile - {ex.Message}");
        }
    }

    private async Task ClearFeedbackAsync(HangStepDefinition? definition, CancellationToken cancellationToken, string successMessage)
    {
        if (definition is null)
        {
            return;
        }

        var settings = esp32SettingsService.Load();
        try
        {
            var response = await esp32ApiClient.ClearHangFeedbackAsync(settings, cancellationToken);
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
            SetState(new HangExecutionState
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

    private void SetState(HangExecutionState state)
    {
        currentState = state;
        StateChanged?.Invoke(this, state);
    }

    private static void ValidateDefinition(HangStepDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.WallId) || string.IsNullOrWhiteSpace(definition.WallName))
        {
            throw new InvalidOperationException("Seleziona una parete valida per la sospensione.");
        }

        if (definition.DurationSeconds <= 0)
        {
            throw new InvalidOperationException("La durata della sospensione deve essere maggiore di zero.");
        }

        if (definition.TargetHoleNumbers.Count is < 1 or > 2)
        {
            throw new InvalidOperationException("La sospensione richiede da 1 a 2 prese selezionate.");
        }
    }

    private static string BuildTargetsText(IEnumerable<int> targetHoleNumbers)
    {
        return $"fori {string.Join(", ", targetHoleNumbers.OrderBy(value => value))}";
    }
}
