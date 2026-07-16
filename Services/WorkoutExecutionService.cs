using RuoteLab.Models;

namespace RuoteLab.Services;

public sealed class WorkoutExecutionService : IWorkoutExecutionService
{
    private enum ActiveExecutor
    {
        None,
        Rest,
        Resistance,
        Hang,
        Circuit
    }

    private readonly IRestExecutionService restExecutionService;
    private readonly IResistanceExecutionService resistanceExecutionService;
    private readonly IHangExecutionService hangExecutionService;
    private readonly IEsp32ApiClient esp32ApiClient;
    private readonly IEsp32SettingsService esp32SettingsService;
    private readonly IEsp32PayloadBuilderService esp32PayloadBuilderService;
    private readonly ICircuitRepository circuitRepository;
    private readonly IRoomRepository roomRepository;
    private readonly IWallRepository wallRepository;
    private readonly SemaphoreSlim gate = new(1, 1);

    private WorkoutDefinition? currentWorkout;
    private List<WorkoutExpandedPhaseDefinition> currentPlan = new();
    private CancellationTokenSource? runCancellationSource;
    private ActiveExecutor activeExecutor;
    private WorkoutExecutionState currentState = new()
    {
        Status = WorkoutExecutionStatus.Idle,
        StatusMessage = "Allenamento pronto."
    };

    public WorkoutExecutionService(
        IRestExecutionService restExecutionService,
        IResistanceExecutionService resistanceExecutionService,
        IHangExecutionService hangExecutionService,
        IEsp32ApiClient esp32ApiClient,
        IEsp32SettingsService esp32SettingsService,
        IEsp32PayloadBuilderService esp32PayloadBuilderService,
        ICircuitRepository circuitRepository,
        IRoomRepository roomRepository,
        IWallRepository wallRepository)
    {
        this.restExecutionService = restExecutionService;
        this.resistanceExecutionService = resistanceExecutionService;
        this.hangExecutionService = hangExecutionService;
        this.esp32ApiClient = esp32ApiClient;
        this.esp32SettingsService = esp32SettingsService;
        this.esp32PayloadBuilderService = esp32PayloadBuilderService;
        this.circuitRepository = circuitRepository;
        this.roomRepository = roomRepository;
        this.wallRepository = wallRepository;
    }

    public WorkoutExecutionState CurrentState => currentState;

    public event EventHandler<WorkoutExecutionState>? StateChanged;

    public IReadOnlyList<WorkoutExpandedPhaseDefinition> BuildExecutionPlan(WorkoutDefinition workout)
    {
        ValidateWorkout(workout);

        var plan = new List<WorkoutExpandedPhaseDefinition>();
        for (var stepIndex = 0; stepIndex < workout.Steps.Count; stepIndex++)
        {
            var step = workout.Steps[stepIndex];
            for (var repetition = 1; repetition <= step.Repetitions; repetition++)
            {
                if (step.InitialRestSeconds > 0)
                {
                    plan.Add(new WorkoutExpandedPhaseDefinition
                    {
                        PlanIndex = plan.Count,
                        StepIndex = stepIndex,
                        StepId = step.StepId,
                        StepName = step.Name,
                        StepType = step.StepType,
                        Phase = WorkoutRuntimePhase.InitialRest,
                        Repetition = repetition,
                        TotalRepetitions = step.Repetitions,
                        DurationSeconds = step.InitialRestSeconds
                    });
                }

                plan.Add(new WorkoutExpandedPhaseDefinition
                {
                    PlanIndex = plan.Count,
                    StepIndex = stepIndex,
                    StepId = step.StepId,
                    StepName = step.Name,
                    StepType = step.StepType,
                    Phase = WorkoutRuntimePhase.Work,
                    Repetition = repetition,
                    TotalRepetitions = step.Repetitions,
                    DurationSeconds = step.WorkSeconds
                });

                if (step.FinalRestSeconds > 0)
                {
                    plan.Add(new WorkoutExpandedPhaseDefinition
                    {
                        PlanIndex = plan.Count,
                        StepIndex = stepIndex,
                        StepId = step.StepId,
                        StepName = step.Name,
                        StepType = step.StepType,
                        Phase = WorkoutRuntimePhase.FinalRest,
                        Repetition = repetition,
                        TotalRepetitions = step.Repetitions,
                        DurationSeconds = step.FinalRestSeconds
                    });
                }
            }
        }

        return plan;
    }

    public async Task StartAsync(WorkoutDefinition workout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workout);

        List<WorkoutExpandedPhaseDefinition> plan;
        CancellationTokenSource localRunCancellationSource;

        await gate.WaitAsync(cancellationToken);
        try
        {
            currentPlan = BuildExecutionPlan(workout).ToList();
            currentWorkout = workout;

            runCancellationSource?.Cancel();
            runCancellationSource?.Dispose();
            runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localRunCancellationSource = runCancellationSource;
            activeExecutor = ActiveExecutor.None;

            SetState(new WorkoutExecutionState
            {
                Status = WorkoutExecutionStatus.Running,
                WorkoutId = workout.WorkoutId,
                CurrentStepIndex = currentPlan.Count == 0 ? -1 : currentPlan[0].StepIndex,
                CurrentPlanIndex = currentPlan.Count == 0 ? -1 : 0,
                CurrentRepetition = currentPlan.Count == 0 ? 0 : currentPlan[0].Repetition,
                CurrentPhase = currentPlan.Count == 0 ? WorkoutRuntimePhase.InitialRest : currentPlan[0].Phase,
                CurrentStepName = currentPlan.Count == 0 ? string.Empty : currentPlan[0].StepName,
                CurrentStepType = currentPlan.Count == 0 ? null : currentPlan[0].StepType,
                RemainingSeconds = currentPlan.Count == 0 ? 0 : currentPlan[0].DurationSeconds,
                TotalSteps = workout.Steps.Count,
                TotalPhases = currentPlan.Count,
                StartedAtUtc = DateTimeOffset.UtcNow,
                StatusMessage = currentPlan.Count == 0 ? "Allenamento vuoto." : BuildPhaseStatusMessage(currentPlan[0])
            });

            plan = currentPlan.ToList();
        }
        finally
        {
            gate.Release();
        }

        _ = RunPlanAsync(workout, plan, localRunCancellationSource.Token);
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != WorkoutExecutionStatus.Running)
            {
                return;
            }

            SetState(new WorkoutExecutionState
            {
                Status = WorkoutExecutionStatus.Paused,
                WorkoutId = currentState.WorkoutId,
                CurrentStepIndex = currentState.CurrentStepIndex,
                CurrentPlanIndex = currentState.CurrentPlanIndex,
                CurrentRepetition = currentState.CurrentRepetition,
                CurrentPhase = currentState.CurrentPhase,
                CurrentStepName = currentState.CurrentStepName,
                CurrentStepType = currentState.CurrentStepType,
                RemainingSeconds = currentState.RemainingSeconds,
                TotalSteps = currentState.TotalSteps,
                TotalPhases = currentState.TotalPhases,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = "Allenamento in pausa."
            });
        }
        finally
        {
            gate.Release();
        }

        switch (activeExecutor)
        {
            case ActiveExecutor.Rest:
                await restExecutionService.PauseAsync(cancellationToken);
                break;
            case ActiveExecutor.Resistance:
                await resistanceExecutionService.PauseAsync(cancellationToken);
                break;
            case ActiveExecutor.Hang:
                await hangExecutionService.PauseAsync(cancellationToken);
                break;
            case ActiveExecutor.Circuit:
                await StopCircuitFeedbackSafeAsync(cancellationToken);
                break;
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status != WorkoutExecutionStatus.Paused)
            {
                return;
            }

            SetState(new WorkoutExecutionState
            {
                Status = WorkoutExecutionStatus.Running,
                WorkoutId = currentState.WorkoutId,
                CurrentStepIndex = currentState.CurrentStepIndex,
                CurrentPlanIndex = currentState.CurrentPlanIndex,
                CurrentRepetition = currentState.CurrentRepetition,
                CurrentPhase = currentState.CurrentPhase,
                CurrentStepName = currentState.CurrentStepName,
                CurrentStepType = currentState.CurrentStepType,
                RemainingSeconds = currentState.RemainingSeconds,
                TotalSteps = currentState.TotalSteps,
                TotalPhases = currentState.TotalPhases,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = null,
                StatusMessage = currentState.StatusMessage
            });
        }
        finally
        {
            gate.Release();
        }

        switch (activeExecutor)
        {
            case ActiveExecutor.Rest:
                await restExecutionService.ResumeAsync(cancellationToken);
                break;
            case ActiveExecutor.Resistance:
                await resistanceExecutionService.ResumeAsync(cancellationToken);
                break;
            case ActiveExecutor.Hang:
                await hangExecutionService.ResumeAsync(cancellationToken);
                break;
            case ActiveExecutor.Circuit:
                break;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (currentState.Status == WorkoutExecutionStatus.Idle)
            {
                return;
            }

            runCancellationSource?.Cancel();
            SetState(new WorkoutExecutionState
            {
                Status = WorkoutExecutionStatus.Cancelled,
                WorkoutId = currentState.WorkoutId,
                CurrentStepIndex = currentState.CurrentStepIndex,
                CurrentPlanIndex = currentState.CurrentPlanIndex,
                CurrentRepetition = currentState.CurrentRepetition,
                CurrentPhase = currentState.CurrentPhase,
                CurrentStepName = currentState.CurrentStepName,
                CurrentStepType = currentState.CurrentStepType,
                RemainingSeconds = currentState.RemainingSeconds,
                TotalSteps = currentState.TotalSteps,
                TotalPhases = currentState.TotalPhases,
                StartedAtUtc = currentState.StartedAtUtc,
                StatusMessage = "Allenamento terminato manualmente."
            });
        }
        finally
        {
            gate.Release();
        }

        switch (activeExecutor)
        {
            case ActiveExecutor.Rest:
                await restExecutionService.StopAsync(cancellationToken);
                break;
            case ActiveExecutor.Resistance:
                await resistanceExecutionService.StopAsync(cancellationToken);
                break;
            case ActiveExecutor.Hang:
                await hangExecutionService.StopAsync(cancellationToken);
                break;
            case ActiveExecutor.Circuit:
                await StopCircuitFeedbackSafeAsync(cancellationToken);
                break;
        }
    }

    private async Task RunPlanAsync(WorkoutDefinition workout, IReadOnlyList<WorkoutExpandedPhaseDefinition> plan, CancellationToken cancellationToken)
    {
        if (plan.Count == 0)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                SetState(new WorkoutExecutionState
                {
                    Status = WorkoutExecutionStatus.Completed,
                    WorkoutId = workout.WorkoutId,
                    CurrentStepIndex = -1,
                    CurrentPlanIndex = -1,
                    CurrentRepetition = 0,
                    CurrentPhase = WorkoutRuntimePhase.InitialRest,
                    CurrentStepName = string.Empty,
                    CurrentStepType = null,
                    RemainingSeconds = 0,
                    TotalSteps = workout.Steps.Count,
                    TotalPhases = 0,
                    StartedAtUtc = currentState.StartedAtUtc,
                    EndsAtUtc = DateTimeOffset.UtcNow,
                    StatusMessage = "Allenamento completato."
                });
            }
            finally
            {
                gate.Release();
            }

            return;
        }

        for (var index = 0; index < plan.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var phase = plan[index];
            var step = workout.Steps[phase.StepIndex];
            await UpdatePhaseStateAsync(workout, phase, cancellationToken);
            await ExecutePhaseAsync(workout, step, phase, cancellationToken);
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            activeExecutor = ActiveExecutor.None;
            SetState(new WorkoutExecutionState
            {
                Status = WorkoutExecutionStatus.Completed,
                WorkoutId = workout.WorkoutId,
                CurrentStepIndex = workout.Steps.Count - 1,
                CurrentPlanIndex = plan.Count - 1,
                CurrentRepetition = plan[^1].Repetition,
                CurrentPhase = plan[^1].Phase,
                CurrentStepName = plan[^1].StepName,
                CurrentStepType = plan[^1].StepType,
                RemainingSeconds = 0,
                TotalSteps = workout.Steps.Count,
                TotalPhases = plan.Count,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = DateTimeOffset.UtcNow,
                StatusMessage = "Allenamento completato."
            });
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task UpdatePhaseStateAsync(WorkoutDefinition workout, WorkoutExpandedPhaseDefinition phase, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            SetState(new WorkoutExecutionState
            {
                Status = WorkoutExecutionStatus.Running,
                WorkoutId = workout.WorkoutId,
                CurrentStepIndex = phase.StepIndex,
                CurrentPlanIndex = phase.PlanIndex,
                CurrentRepetition = phase.Repetition,
                CurrentPhase = phase.Phase,
                CurrentStepName = phase.StepName,
                CurrentStepType = phase.StepType,
                RemainingSeconds = phase.DurationSeconds,
                TotalSteps = workout.Steps.Count,
                TotalPhases = currentPlan.Count,
                StartedAtUtc = currentState.StartedAtUtc == default ? DateTimeOffset.UtcNow : currentState.StartedAtUtc,
                StatusMessage = BuildPhaseStatusMessage(phase)
            });
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task ExecutePhaseAsync(WorkoutDefinition workout, WorkoutStepDefinition step, WorkoutExpandedPhaseDefinition phase, CancellationToken cancellationToken)
    {
        switch (phase.Phase)
        {
            case WorkoutRuntimePhase.InitialRest:
            case WorkoutRuntimePhase.FinalRest:
                activeExecutor = ActiveExecutor.Rest;
                await restExecutionService.StartAsync(BuildRestDefinitionForPhase(workout, step, phase), cancellationToken);
                await WaitForRestCompletionAsync(cancellationToken);
                break;
            case WorkoutRuntimePhase.Work when step.StepType == WorkoutStepType.Rest:
                activeExecutor = ActiveExecutor.Rest;
                await restExecutionService.StartAsync(BuildRestDefinitionForPhase(workout, step, phase), cancellationToken);
                await WaitForRestCompletionAsync(cancellationToken);
                break;
            case WorkoutRuntimePhase.Work when step.StepType == WorkoutStepType.Resistance:
                activeExecutor = ActiveExecutor.Resistance;
                await resistanceExecutionService.StartAsync(BuildResistanceDefinitionForPhase(workout, step, phase), cancellationToken);
                await WaitForResistanceCompletionAsync(cancellationToken);
                break;
            case WorkoutRuntimePhase.Work when step.StepType == WorkoutStepType.Hang:
                activeExecutor = ActiveExecutor.Hang;
                await hangExecutionService.StartAsync(BuildHangDefinitionForPhase(workout, step, phase), cancellationToken);
                await WaitForHangCompletionAsync(cancellationToken);
                break;
            case WorkoutRuntimePhase.Work when step.StepType == WorkoutStepType.Circuit:
                activeExecutor = ActiveExecutor.Circuit;
                await ExecuteCircuitPhaseAsync(workout, step, phase, cancellationToken);
                break;
            default:
                throw new InvalidOperationException("Fase workout non supportata.");
        }
    }

    private async Task ExecuteCircuitPhaseAsync(WorkoutDefinition workout, WorkoutStepDefinition step, WorkoutExpandedPhaseDefinition phase, CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        var circuit = await EnsureCircuitReadyAsync(workout, step, settings, cancellationToken);
        var circuitPayload = step.CircuitPayload ?? throw new InvalidOperationException("Payload circuito mancante.");
        var remaining = phase.DurationSeconds;
        var commandRunning = false;

        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (currentState.Status == WorkoutExecutionStatus.Paused)
            {
                if (commandRunning)
                {
                    await StopCircuitFeedbackSafeAsync(cancellationToken);
                    commandRunning = false;
                }

                await Task.Delay(200, cancellationToken);
                continue;
            }

            if (!commandRunning)
            {
                await SendCircuitCommandAsync(settings, circuitPayload.Mode, circuit, cancellationToken);
                commandRunning = true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            remaining = Math.Max(0, remaining - 1);
            await UpdateCurrentRemainingAsync(remaining, cancellationToken);
        }

        if (commandRunning)
        {
            await StopCircuitFeedbackSafeAsync(cancellationToken);
        }
    }

    private Task WaitForRestCompletionAsync(CancellationToken cancellationToken)
    {
        return WaitForChildCompletionAsync<RestExecutionState>(
            handler => restExecutionService.StateChanged += handler,
            handler => restExecutionService.StateChanged -= handler,
            state => state.Status is RestExecutionStatus.Completed or RestExecutionStatus.Cancelled,
            cancellationToken);
    }

    private Task WaitForResistanceCompletionAsync(CancellationToken cancellationToken)
    {
        return WaitForChildCompletionAsync<ResistanceExecutionState>(
            handler => resistanceExecutionService.StateChanged += handler,
            handler => resistanceExecutionService.StateChanged -= handler,
            state => state.Status is ResistanceExecutionStatus.Completed or ResistanceExecutionStatus.Cancelled,
            cancellationToken);
    }

    private Task WaitForHangCompletionAsync(CancellationToken cancellationToken)
    {
        return WaitForChildCompletionAsync<HangExecutionState>(
            handler => hangExecutionService.StateChanged += handler,
            handler => hangExecutionService.StateChanged -= handler,
            state => state.Status is HangExecutionStatus.Completed or HangExecutionStatus.Cancelled,
            cancellationToken);
    }

    private static Task WaitForChildCompletionAsync<TState>(
        Action<EventHandler<TState>> subscribe,
        Action<EventHandler<TState>> unsubscribe,
        Func<TState, bool> predicate,
        CancellationToken cancellationToken)
    {
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<TState>? handler = null;
        CancellationTokenRegistration registration = default;

        handler = (_, state) =>
        {
            if (!predicate(state))
            {
                return;
            }

            unsubscribe(handler!);
            registration.Dispose();
            completionSource.TrySetResult();
        };

        registration = cancellationToken.Register(() =>
        {
            unsubscribe(handler!);
            completionSource.TrySetCanceled(cancellationToken);
        });

        subscribe(handler!);
        return completionSource.Task;
    }

    private static RestStepDefinition BuildRestDefinitionForPhase(WorkoutDefinition workout, WorkoutStepDefinition step, WorkoutExpandedPhaseDefinition phase)
    {
        var payload = step.RestPayload;
        return new RestStepDefinition
        {
            Name = $"{step.Name} - {GetPhaseLabel(phase.Phase)}",
            RoomName = workout.RoomName,
            WallId = workout.WallId,
            WallName = workout.WallName,
            DurationSeconds = phase.DurationSeconds,
            BlinkColor = payload?.BlinkColor ?? "#FF0000",
            CompletedColor = payload?.CompletedColor ?? "#00FF00",
            BlinkPeriodMs = payload?.BlinkPeriodMs ?? 500,
            CompletedHoldSeconds = payload?.CompletedHoldSeconds ?? 3
        };
    }

    private static ResistanceStepDefinition BuildResistanceDefinitionForPhase(WorkoutDefinition workout, WorkoutStepDefinition step, WorkoutExpandedPhaseDefinition phase)
    {
        var payload = step.ResistancePayload;
        return new ResistanceStepDefinition
        {
            Name = step.Name,
            RoomName = workout.RoomName,
            WallId = workout.WallId,
            WallName = workout.WallName,
            DurationSeconds = phase.DurationSeconds,
            ActiveMode = string.IsNullOrWhiteSpace(payload?.ActiveMode) ? "steady" : payload!.ActiveMode,
            ActiveColor = payload?.ActiveColor ?? "#FF8C00",
            CompletedColor = payload?.CompletedColor ?? "#00FF00",
            BlinkPeriodMs = payload?.BlinkPeriodMs ?? 1000,
            CompletedHoldSeconds = payload?.CompletedHoldSeconds ?? 3
        };
    }

    private static HangStepDefinition BuildHangDefinitionForPhase(WorkoutDefinition workout, WorkoutStepDefinition step, WorkoutExpandedPhaseDefinition phase)
    {
        var payload = step.HangPayload ?? throw new InvalidOperationException("Payload hang mancante.");
        return new HangStepDefinition
        {
            Name = step.Name,
            RoomName = workout.RoomName,
            WallId = workout.WallId,
            WallName = workout.WallName,
            DurationSeconds = phase.DurationSeconds,
            TargetHoleNumbers = payload.TargetHoleNumbers.ToList(),
            ActiveColor = payload.ActiveColor ?? "#00BFFF",
            CompletedColor = payload.CompletedColor ?? "#00FF00",
            CompletedHoldSeconds = payload.CompletedHoldSeconds ?? 3
        };
    }

    private async Task<CircuitDefinition> EnsureCircuitReadyAsync(WorkoutDefinition workout, WorkoutStepDefinition step, Esp32DeviceSettings settings, CancellationToken cancellationToken)
    {
        var circuitPayload = step.CircuitPayload ?? throw new InvalidOperationException("Payload circuito mancante.");
        var allWalls = await wallRepository.GetAllAsync(cancellationToken);
        var selectedWall = allWalls.FirstOrDefault(wall => string.Equals(Esp32PayloadBuilderService.BuildWallId(wall), workout.WallId, StringComparison.Ordinal))
                           ?? throw new InvalidOperationException($"Parete workout non trovata per wallId {workout.WallId}.");

        var allRooms = await roomRepository.GetAllAsync(cancellationToken);
        var room = allRooms.FirstOrDefault(item => string.Equals(item.Name, selectedWall.RoomName, StringComparison.Ordinal))
                   ?? throw new InvalidOperationException("Sala della parete workout non disponibile.");

        var allCircuits = await circuitRepository.GetAllAsync(cancellationToken);
        var localCircuits = allCircuits
            .Where(circuit =>
                string.Equals(circuit.RoomName, selectedWall.RoomName, StringComparison.Ordinal) &&
                string.Equals(circuit.WallName, selectedWall.Name, StringComparison.Ordinal))
            .ToList();

        var selectedCircuit = localCircuits.FirstOrDefault(circuit =>
                                 string.Equals(Esp32PayloadBuilderService.BuildCircuitId(circuit), circuitPayload.CircuitId, StringComparison.Ordinal)) ??
                             localCircuits.FirstOrDefault(circuit =>
                                 string.Equals(circuit.Name, circuitPayload.CircuitName, StringComparison.Ordinal));

        if (selectedCircuit is null)
        {
            throw new InvalidOperationException($"Circuito workout non trovato: {circuitPayload.CircuitName}.");
        }

        var wallId = Esp32PayloadBuilderService.BuildWallId(selectedWall);
        var statusResponse = await esp32ApiClient.GetStatusAsync(settings, cancellationToken);
        if (!statusResponse.Success)
        {
            throw new InvalidOperationException($"Status ESP32 non disponibile: {statusResponse.ErrorCode} - {statusResponse.Message}");
        }

        var requiresCircuitSync = !string.Equals(statusResponse.Data?.ConfiguredWallId, wallId, StringComparison.Ordinal);
        if (requiresCircuitSync)
        {
            var wallPayload = esp32PayloadBuilderService.BuildWallConfig(selectedWall, room, settings);
            var configResponse = await esp32ApiClient.PostConfigAsync(settings, wallPayload, cancellationToken);
            if (!configResponse.Success)
            {
                throw new InvalidOperationException($"Invio config parete fallito: {configResponse.ErrorCode} - {configResponse.Message}");
            }
        }

        if (!requiresCircuitSync)
        {
            var remoteCircuitsResponse = await esp32ApiClient.GetCircuitsAsync(settings, cancellationToken);
            if (!remoteCircuitsResponse.Success)
            {
                requiresCircuitSync = true;
            }
            else
            {
                requiresCircuitSync = !HasSameCircuitCatalog(remoteCircuitsResponse.Data, wallId, localCircuits);
            }
        }

        if (requiresCircuitSync)
        {
            var circuitsPayload = esp32PayloadBuilderService.BuildCircuitsPayload(selectedWall, room, localCircuits);
            var circuitsResponse = await esp32ApiClient.PostCircuitsAsync(settings, circuitsPayload, cancellationToken);
            if (!circuitsResponse.Success)
            {
                throw new InvalidOperationException($"Sync circuiti fallita: {circuitsResponse.ErrorCode} - {circuitsResponse.Message}");
            }
        }

        return selectedCircuit;
    }

    private async Task SendCircuitCommandAsync(Esp32DeviceSettings settings, WorkoutCircuitMode mode, CircuitDefinition circuit, CancellationToken cancellationToken)
    {
        var circuitId = Esp32PayloadBuilderService.BuildCircuitId(circuit);
        var response = mode switch
        {
            WorkoutCircuitMode.Visualize => await esp32ApiClient.VisualizeCircuitAsync(settings, circuitId, cancellationToken),
            _ => await esp32ApiClient.StartCircuitAsync(settings, circuitId, cancellationToken)
        };

        if (!response.Success)
        {
            throw new InvalidOperationException($"Comando circuito fallito: {response.ErrorCode} - {response.Message}");
        }
    }

    private async Task StopCircuitFeedbackSafeAsync(CancellationToken cancellationToken)
    {
        var settings = esp32SettingsService.Load();
        try
        {
            await esp32ApiClient.StopCircuitAsync(settings, cancellationToken);
        }
        catch
        {
        }
    }

    private async Task UpdateCurrentRemainingAsync(int remainingSeconds, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            SetState(new WorkoutExecutionState
            {
                Status = currentState.Status,
                WorkoutId = currentState.WorkoutId,
                CurrentStepIndex = currentState.CurrentStepIndex,
                CurrentPlanIndex = currentState.CurrentPlanIndex,
                CurrentRepetition = currentState.CurrentRepetition,
                CurrentPhase = currentState.CurrentPhase,
                CurrentStepName = currentState.CurrentStepName,
                CurrentStepType = currentState.CurrentStepType,
                RemainingSeconds = remainingSeconds,
                TotalSteps = currentState.TotalSteps,
                TotalPhases = currentState.TotalPhases,
                StartedAtUtc = currentState.StartedAtUtc,
                EndsAtUtc = currentState.EndsAtUtc,
                StatusMessage = currentState.StatusMessage
            });
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool HasSameCircuitCatalog(Esp32CircuitsCatalogData? remoteCatalog, string expectedWallId, IReadOnlyList<CircuitDefinition> localCircuits)
    {
        if (remoteCatalog is null || !string.Equals(remoteCatalog.WallId, expectedWallId, StringComparison.Ordinal))
        {
            return false;
        }

        var remoteIds = remoteCatalog.Circuits
            .Select(item => item.CircuitId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
        var localIds = localCircuits
            .Select(Esp32PayloadBuilderService.BuildCircuitId)
            .ToHashSet(StringComparer.Ordinal);

        return remoteIds.SetEquals(localIds);
    }

    private void SetState(WorkoutExecutionState state)
    {
        currentState = state;
        StateChanged?.Invoke(this, state);
    }

    private static string BuildPhaseStatusMessage(WorkoutExpandedPhaseDefinition phase)
    {
        return $"{phase.StepName} - {GetPhaseLabel(phase.Phase)} - ripetizione {phase.Repetition}/{phase.TotalRepetitions}";
    }

    private static string GetPhaseLabel(WorkoutRuntimePhase phase)
    {
        return phase switch
        {
            WorkoutRuntimePhase.InitialRest => "rest iniziale",
            WorkoutRuntimePhase.Work => "lavoro",
            WorkoutRuntimePhase.FinalRest => "rest finale",
            _ => "fase"
        };
    }

    private static void ValidateWorkout(WorkoutDefinition workout)
    {
        ArgumentNullException.ThrowIfNull(workout);

        if (string.IsNullOrWhiteSpace(workout.Name))
        {
            throw new InvalidOperationException("L'allenamento deve avere un nome.");
        }

        if (string.IsNullOrWhiteSpace(workout.WallId) || string.IsNullOrWhiteSpace(workout.WallName))
        {
            throw new InvalidOperationException("L'allenamento deve riferirsi a una parete valida.");
        }

        if (workout.Steps.Count == 0)
        {
            throw new InvalidOperationException("L'allenamento deve contenere almeno uno step.");
        }

        for (var index = 0; index < workout.Steps.Count; index++)
        {
            var step = workout.Steps[index];
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                throw new InvalidOperationException($"Lo step {index + 1} deve avere un nome.");
            }

            if (step.WorkSeconds <= 0)
            {
                throw new InvalidOperationException($"Lo step {step.Name} deve avere un tempo di lavoro maggiore di zero.");
            }

            if (step.InitialRestSeconds < 0 || step.FinalRestSeconds < 0)
            {
                throw new InvalidOperationException($"Lo step {step.Name} non puo' avere rest negativi.");
            }

            if (step.Repetitions <= 0)
            {
                throw new InvalidOperationException($"Lo step {step.Name} deve avere almeno una ripetizione.");
            }

            switch (step.StepType)
            {
                case WorkoutStepType.Rest when step.RestPayload is null:
                    break;
                case WorkoutStepType.Resistance when step.ResistancePayload is null:
                    break;
                case WorkoutStepType.Hang:
                    if (step.HangPayload is null || step.HangPayload.TargetHoleNumbers.Count is < 1 or > 2)
                    {
                        throw new InvalidOperationException($"Lo step {step.Name} richiede da 1 a 2 prese target.");
                    }
                    break;
                case WorkoutStepType.Circuit:
                    if (step.CircuitPayload is null || string.IsNullOrWhiteSpace(step.CircuitPayload.CircuitId))
                    {
                        throw new InvalidOperationException($"Lo step {step.Name} richiede un circuito valido.");
                    }
                    break;
            }
        }
    }
}
