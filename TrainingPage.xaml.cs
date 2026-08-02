using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using RouteLab.Drawing;
using RouteLab.Models;
using RouteLab.Services;

namespace RouteLab;

public partial class TrainingPage : ContentPage
{
    private readonly App app;
    private readonly IRestExecutionService restExecutionService;
    private readonly IResistanceExecutionService resistanceExecutionService;
    private readonly IHangExecutionService hangExecutionService;
    private readonly IWorkoutExecutionService workoutExecutionService;
    private readonly CircuitEditorDrawable hangPreviewDrawable = new();
    private IReadOnlyList<WallDefinition> availableWalls = Array.Empty<WallDefinition>();
    private IReadOnlyList<CircuitDefinition> availableCircuits = Array.Empty<CircuitDefinition>();
    private IReadOnlyList<WorkoutDefinition> availableWorkouts = Array.Empty<WorkoutDefinition>();
    private readonly List<int> selectedHangHoleNumbers = new();
    private readonly List<WorkoutStepDefinition> workoutSteps = new();
    private string? selectedRoomName;
    private string? currentWorkoutId;
    private WallDefinition? selectedWall;
    private WallHoleDefinition? highlightedHangHole;
    private int selectedWorkoutStepIndex = -1;
    private bool isRefreshing;
    private bool isLoadingSavedWorkout;
    private double hangPreviewBaseWidth = 320d;
    private double hangPreviewBaseHeight = 320d;

    public TrainingPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
        restExecutionService = app.RestExecutionService;
        resistanceExecutionService = app.ResistanceExecutionService;
        hangExecutionService = app.HangExecutionService;
        workoutExecutionService = app.WorkoutExecutionService;
        HangPreviewCanvas.Drawable = hangPreviewDrawable;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        restExecutionService.StateChanged += OnRestStateChanged;
        resistanceExecutionService.StateChanged += OnResistanceStateChanged;
        hangExecutionService.StateChanged += OnHangStateChanged;
        workoutExecutionService.StateChanged += OnWorkoutStateChanged;

        if (isRefreshing)
        {
            return;
        }

        using var busy = AppBusy.Show("Caricamento allenamenti...");
        try
        {
            isRefreshing = true;
            await app.GymSetupViewModel.EnsureLoadedAsync();
            availableWalls = app.GymSetupViewModel.Walls.OrderBy(wall => wall.RoomName).ThenBy(wall => wall.Name).ToList();
            availableCircuits = (await app.CircuitRepository.GetAllAsync()).ToList();
            availableWorkouts = (await app.WorkoutRepository.GetAllAsync()).ToList();
            selectedRoomName ??= app.GymSetupViewModel.AvailableRoomNames.FirstOrDefault();
            selectedWall ??= GetVisibleWalls().FirstOrDefault();
            if (selectedWall is not null)
            {
                selectedRoomName = selectedWall.RoomName;
            }

            EnsureDefaultsLoaded();
            RefreshView();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    protected override void OnDisappearing()
    {
        restExecutionService.StateChanged -= OnRestStateChanged;
        resistanceExecutionService.StateChanged -= OnResistanceStateChanged;
        hangExecutionService.StateChanged -= OnHangStateChanged;
        workoutExecutionService.StateChanged -= OnWorkoutStateChanged;
        base.OnDisappearing();
    }

    private void EnsureDefaultsLoaded()
    {
        var visuals = app.AppSettingsService.Load().TrainingVisuals;
        DurationSecondsEntry.Text ??= "60";
        BlinkPeriodMsEntry.Text ??= "500";
        CompletedHoldSecondsEntry.Text ??= "3";
        BlinkColorValueLabel.Text = visuals.RestBlinkColor;
        CompletedColorValueLabel.Text = visuals.RestCompletedColor;
        RefreshRestColorPreviews();

        ResistanceDurationSecondsEntry.Text ??= "120";
        ResistanceBlinkPeriodMsEntry.Text ??= "1000";
        ResistanceModePicker.ItemsSource = new List<string> { "steady", "blink" };
        ResistanceModePicker.SelectedItem ??= "steady";
        ResistanceActiveColorValueLabel.Text = visuals.ResistanceActiveColor;
        ResistanceCompletedColorValueLabel.Text = visuals.ResistanceCompletedColor;
        RefreshResistanceColorPreviews();

        HangDurationSecondsEntry.Text ??= "10";
        HangCompletedHoldSecondsEntry.Text ??= "3";
        HangActiveColorValueLabel.Text = visuals.HangActiveColor;
        HangCompletedColorValueLabel.Text = visuals.HangCompletedColor;
        RefreshHangColorPreviews();

        WorkoutNameEntry.Text ??= "Allenamento A";
        WorkoutDescriptionEntry.Text ??= string.Empty;
        WorkoutStepTypePicker.ItemsSource = Enum.GetValues(typeof(WorkoutStepType)).Cast<WorkoutStepType>().ToList();
        WorkoutStepTypePicker.SelectedItem ??= WorkoutStepType.Rest;
        WorkoutStepNameEntry.Text ??= "Step 1";
        WorkoutStepWorkSecondsEntry.Text ??= "20";
        WorkoutStepInitialRestSecondsEntry.Text ??= "0";
        WorkoutStepFinalRestSecondsEntry.Text ??= "0";
        WorkoutStepRepetitionsEntry.Text ??= "1";
        WorkoutCircuitModePicker.ItemsSource = Enum.GetValues(typeof(WorkoutCircuitMode)).Cast<WorkoutCircuitMode>().ToList();
        WorkoutCircuitModePicker.SelectedItem ??= WorkoutCircuitMode.Start;
        RefreshWorkoutPayloadSummaries();
        UpdateWorkoutStepBuilderVisibility();
    }

    private void OnRoomChanged(object? sender, EventArgs e)
    {
        selectedRoomName = RoomPicker.SelectedItem as string;
        selectedWall = GetVisibleWalls().FirstOrDefault();
        selectedHangHoleNumbers.Clear();
        highlightedHangHole = null;
        RefreshView();
    }

    private void OnWallChanged(object? sender, EventArgs e)
    {
        selectedWall = WallPicker.SelectedItem as WallDefinition;
        if (selectedWall is not null)
        {
            selectedRoomName = selectedWall.RoomName;
        }

        selectedHangHoleNumbers.Clear();
        highlightedHangHole = null;
        RefreshView();
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        try
        {
            await restExecutionService.StartAsync(BuildRestDefinitionFromEditor());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Allenamento", ex.Message, "OK");
        }
    }

    private async void OnPauseClicked(object? sender, EventArgs e)
    {
        await restExecutionService.PauseAsync();
    }

    private async void OnResumeClicked(object? sender, EventArgs e)
    {
        await restExecutionService.ResumeAsync();
    }

    private async void OnStopClicked(object? sender, EventArgs e)
    {
        await restExecutionService.StopAsync();
    }

    private async void OnResistanceStartClicked(object? sender, EventArgs e)
    {
        try
        {
            await resistanceExecutionService.StartAsync(BuildResistanceDefinitionFromEditor());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Allenamento", ex.Message, "OK");
        }
    }

    private async void OnResistancePauseClicked(object? sender, EventArgs e)
    {
        await resistanceExecutionService.PauseAsync();
    }

    private async void OnResistanceResumeClicked(object? sender, EventArgs e)
    {
        await resistanceExecutionService.ResumeAsync();
    }

    private async void OnResistanceStopClicked(object? sender, EventArgs e)
    {
        await resistanceExecutionService.StopAsync();
    }

    private async void OnHangStartClicked(object? sender, EventArgs e)
    {
        try
        {
            await hangExecutionService.StartAsync(BuildHangDefinitionFromEditor());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Allenamento", ex.Message, "OK");
        }
    }

    private async void OnHangPauseClicked(object? sender, EventArgs e)
    {
        await hangExecutionService.PauseAsync();
    }

    private async void OnHangResumeClicked(object? sender, EventArgs e)
    {
        await hangExecutionService.ResumeAsync();
    }

    private async void OnHangStopClicked(object? sender, EventArgs e)
    {
        await hangExecutionService.StopAsync();
    }

    private void OnWorkoutStepTypeChanged(object? sender, EventArgs e)
    {
        UpdateWorkoutStepBuilderVisibility();
    }

    private void OnSavedWorkoutChanged(object? sender, EventArgs e)
    {
        if (isLoadingSavedWorkout)
        {
            return;
        }

        if (SavedWorkoutPicker.SelectedItem is WorkoutDefinition workout)
        {
            LoadWorkoutIntoEditor(workout);
        }
    }

    private void OnNewWorkoutClicked(object? sender, EventArgs e)
    {
        ResetWorkoutEditor();
        RefreshView();
    }

    private async void OnHangPreviewTapped(object? sender, TappedEventArgs e)
    {
        if (selectedWall is null)
        {
            return;
        }

        var position = e.GetPosition(HangPreviewCanvas);
        if (position is null)
        {
            return;
        }

        var hole = hangPreviewDrawable.FindNearestHole(position.Value);
        if (hole is null)
        {
            return;
        }

        if (hole is not WallHoleDefinition targetHole)
        {
            return;
        }

        highlightedHangHole = targetHole;
        if (selectedHangHoleNumbers.Contains(targetHole.Number))
        {
            selectedHangHoleNumbers.Remove(targetHole.Number);
        }
        else
        {
            if (selectedHangHoleNumbers.Count >= 2)
            {
                await DisplayAlertAsync("Sospensione", "Massimo 2 prese per la sospensione.", "OK");
            }
            else
            {
                selectedHangHoleNumbers.Add(targetHole.Number);
            }
        }

        RefreshHangPreview();
        UpdateHangSelectionLabel();
    }

    private void OnHangPreviewSizeChanged(object? sender, EventArgs e)
    {
        if (HangPreviewCanvas.Width <= 0 || HangPreviewCanvas.Height <= 0)
        {
            return;
        }

        hangPreviewBaseWidth = Math.Max(280d, HangPreviewCanvas.Width);
        hangPreviewBaseHeight = Math.Max(280d, HangPreviewCanvas.Height);
        UpdateHangPreviewScale();
    }

    private void OnRestStateChanged(object? sender, RestExecutionState state)
    {
        MainThread.BeginInvokeOnMainThread(() => ApplyRestRuntimeState(state));
    }

    private void OnResistanceStateChanged(object? sender, ResistanceExecutionState state)
    {
        MainThread.BeginInvokeOnMainThread(() => ApplyResistanceRuntimeState(state));
    }

    private void OnHangStateChanged(object? sender, HangExecutionState state)
    {
        MainThread.BeginInvokeOnMainThread(() => ApplyHangRuntimeState(state));
    }

    private void OnWorkoutStateChanged(object? sender, WorkoutExecutionState state)
    {
        MainThread.BeginInvokeOnMainThread(() => ApplyWorkoutRuntimeState(state));
    }

    private async void OnAddWorkoutStepClicked(object? sender, EventArgs e)
    {
        try
        {
            var step = BuildWorkoutStepFromEditor();
            workoutSteps.Add(step);
            selectedWorkoutStepIndex = workoutSteps.Count - 1;
            WorkoutEditorStatusLabel.Text = $"Step aggiunto: {step.Name}";
            RebuildWorkoutStepsList();
            RefreshWorkoutPlanPreview();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Allenamento", ex.Message, "OK");
        }
    }

    private void OnRemoveWorkoutStepClicked(object? sender, EventArgs e)
    {
        if (selectedWorkoutStepIndex < 0 || selectedWorkoutStepIndex >= workoutSteps.Count)
        {
            WorkoutEditorStatusLabel.Text = "Seleziona prima uno step da rimuovere.";
            return;
        }

        var removedName = workoutSteps[selectedWorkoutStepIndex].Name;
        workoutSteps.RemoveAt(selectedWorkoutStepIndex);
        selectedWorkoutStepIndex = workoutSteps.Count == 0
            ? -1
            : Math.Clamp(selectedWorkoutStepIndex, 0, workoutSteps.Count - 1);
        WorkoutEditorStatusLabel.Text = $"Step rimosso: {removedName}";
        RebuildWorkoutStepsList();
        RefreshWorkoutPlanPreview();
    }

    private async void OnSaveWorkoutClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Salvataggio allenamento...");
        try
        {
            var workout = await SaveCurrentWorkoutAsync();
            WorkoutEditorStatusLabel.Text = $"Allenamento salvato: {workout.Name}";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Allenamento", ex.Message, "OK");
        }
    }

    private async void OnDeleteWorkoutClicked(object? sender, EventArgs e)
    {
        if (SavedWorkoutPicker.SelectedItem is not WorkoutDefinition workout)
        {
            WorkoutEditorStatusLabel.Text = "Seleziona prima un allenamento salvato.";
            return;
        }

        var confirm = await DisplayAlertAsync(
            "Conferma eliminazione",
            $"Eliminare l'allenamento salvato '{workout.Name}'?",
            "Elimina",
            "Annulla");

        if (!confirm)
        {
            return;
        }

        using var busy = AppBusy.Show("Eliminazione allenamento...");
        await app.WorkoutRepository.DeleteAsync(workout.WorkoutId);
        if (string.Equals(currentWorkoutId, workout.WorkoutId, StringComparison.Ordinal))
        {
            ResetWorkoutEditor();
        }

        availableWorkouts = (await app.WorkoutRepository.GetAllAsync()).ToList();
        RefreshView();
        WorkoutEditorStatusLabel.Text = $"Allenamento eliminato: {workout.Name}";
    }

    private async void OnUpdateWorkoutStepClicked(object? sender, EventArgs e)
    {
        if (selectedWorkoutStepIndex < 0 || selectedWorkoutStepIndex >= workoutSteps.Count)
        {
            WorkoutEditorStatusLabel.Text = "Seleziona prima uno step da aggiornare.";
            return;
        }

        try
        {
            var existingStepId = workoutSteps[selectedWorkoutStepIndex].StepId;
            var updatedStep = BuildWorkoutStepFromEditor(existingStepId);
            workoutSteps[selectedWorkoutStepIndex] = updatedStep;
            WorkoutEditorStatusLabel.Text = $"Step aggiornato: {updatedStep.Name}";
            RebuildWorkoutStepsList();
            RefreshWorkoutPlanPreview();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Allenamento", ex.Message, "OK");
        }
    }

    private void OnMoveWorkoutStepUpClicked(object? sender, EventArgs e)
    {
        MoveSelectedWorkoutStep(-1);
    }

    private void OnMoveWorkoutStepDownClicked(object? sender, EventArgs e)
    {
        MoveSelectedWorkoutStep(1);
    }

    private async void OnWorkoutStartClicked(object? sender, EventArgs e)
    {
        try
        {
            var workout = await SaveCurrentWorkoutAsync();
            await workoutExecutionService.StartAsync(workout);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Allenamento", ex.Message, "OK");
        }
    }

    private async void OnWorkoutPauseClicked(object? sender, EventArgs e)
    {
        await workoutExecutionService.PauseAsync();
    }

    private async void OnWorkoutResumeClicked(object? sender, EventArgs e)
    {
        await workoutExecutionService.ResumeAsync();
    }

    private async void OnWorkoutStopClicked(object? sender, EventArgs e)
    {
        await workoutExecutionService.StopAsync();
    }

    private void RefreshView()
    {
        var rooms = app.GymSetupViewModel.AvailableRoomNames.ToList();
        if (selectedRoomName is null || !rooms.Contains(selectedRoomName, StringComparer.Ordinal))
        {
            selectedRoomName = rooms.FirstOrDefault();
        }

        RoomPicker.ItemsSource = rooms;
        RoomPicker.SelectedItem = selectedRoomName;

        var visibleWalls = GetVisibleWalls().ToList();
        if (selectedWall is null || !visibleWalls.Any(wall => wall.Id == selectedWall.Id))
        {
            selectedWall = visibleWalls.FirstOrDefault();
        }

        WallPicker.ItemsSource = visibleWalls;
        WallPicker.SelectedItem = selectedWall;
        var wallText = selectedWall is null
            ? "Nessuna parete selezionata."
            : $"Parete attiva: {selectedWall.RoomName} - {selectedWall.Name}";
        RuntimeWallLabel.Text = wallText;
        ResistanceRuntimeWallLabel.Text = wallText;
        HangRuntimeWallLabel.Text = wallText;
        WorkoutCircuitPicker.ItemsSource = GetVisibleCircuits().ToList();
        if (WorkoutCircuitPicker.SelectedItem is not CircuitDefinition selectedCircuit ||
            !GetVisibleCircuits().Any(circuit => circuit.Id == selectedCircuit.Id))
        {
            WorkoutCircuitPicker.SelectedItem = GetVisibleCircuits().FirstOrDefault();
        }

        var visibleWorkouts = GetVisibleWorkouts().ToList();
        SavedWorkoutPicker.ItemsSource = visibleWorkouts;
        if (!visibleWorkouts.Any(workout => string.Equals(workout.WorkoutId, currentWorkoutId, StringComparison.Ordinal)))
        {
            currentWorkoutId = null;
        }

        isLoadingSavedWorkout = true;
        SavedWorkoutPicker.SelectedItem = visibleWorkouts.FirstOrDefault(workout =>
            string.Equals(workout.WorkoutId, currentWorkoutId, StringComparison.Ordinal));
        isLoadingSavedWorkout = false;
        RebuildSavedWorkoutsList(visibleWorkouts);

        RefreshHangPreview();
        UpdateHangSelectionLabel();
        RefreshWorkoutPayloadSummaries();
        UpdateWorkoutStepBuilderVisibility();
        ApplyRestRuntimeState(restExecutionService.CurrentState);
        ApplyResistanceRuntimeState(resistanceExecutionService.CurrentState);
        ApplyHangRuntimeState(hangExecutionService.CurrentState);
        ApplyWorkoutRuntimeState(workoutExecutionService.CurrentState);
        RefreshWorkoutEditorState();
        RebuildWorkoutStepsList();
        RefreshWorkoutPlanPreview();
    }

    private IReadOnlyList<WallDefinition> GetVisibleWalls()
    {
        return availableWalls
            .Where(wall => string.IsNullOrWhiteSpace(selectedRoomName) || string.Equals(wall.RoomName, selectedRoomName, StringComparison.Ordinal))
            .OrderBy(wall => wall.Name)
            .ToList();
    }

    private IReadOnlyList<CircuitDefinition> GetVisibleCircuits()
    {
        if (selectedWall is null)
        {
            return Array.Empty<CircuitDefinition>();
        }

        return availableCircuits
            .Where(circuit =>
                string.Equals(circuit.RoomName, selectedWall.RoomName, StringComparison.Ordinal) &&
                circuit.GetWallNames().Count == 1 &&
                circuit.UsesWall(selectedWall.Name))
            .OrderBy(circuit => circuit.Name)
            .ToList();
    }

    private IReadOnlyList<WorkoutDefinition> GetVisibleWorkouts()
    {
        if (selectedWall is null)
        {
            return Array.Empty<WorkoutDefinition>();
        }

        return availableWorkouts
            .Where(workout =>
                string.Equals(workout.RoomName, selectedWall.RoomName, StringComparison.Ordinal) &&
                string.Equals(workout.WallName, selectedWall.Name, StringComparison.Ordinal))
            .OrderBy(workout => workout.Name)
            .ToList();
    }

    private RestStepDefinition BuildRestDefinitionFromEditor()
    {
        var wall = selectedWall ?? throw new InvalidOperationException("Seleziona prima una parete.");
        return new RestStepDefinition
        {
            Name = "Recupero",
            RoomName = wall.RoomName,
            WallId = Esp32PayloadBuilderService.BuildWallId(wall),
            WallName = wall.Name,
            DurationSeconds = ParsePositiveInt(DurationSecondsEntry.Text, "La durata del recupero deve essere un numero positivo."),
            BlinkPeriodMs = ParseRangeInt(BlinkPeriodMsEntry.Text, 100, 5000, "Il lampeggio deve essere tra 100 e 5000 ms."),
            CompletedHoldSeconds = ParseRangeInt(CompletedHoldSecondsEntry.Text, 0, 30, "La durata verde finale deve essere tra 0 e 30 secondi."),
            BlinkColor = ParseHexColor(BlinkColorValueLabel.Text, "Il colore recupero deve essere in formato #RRGGBB."),
            CompletedColor = ParseHexColor(CompletedColorValueLabel.Text, "Il colore fine recupero deve essere in formato #RRGGBB.")
        };
    }

    private ResistanceStepDefinition BuildResistanceDefinitionFromEditor()
    {
        var wall = selectedWall ?? throw new InvalidOperationException("Seleziona prima una parete.");
        return new ResistanceStepDefinition
        {
            Name = "Resistenza",
            RoomName = wall.RoomName,
            WallId = Esp32PayloadBuilderService.BuildWallId(wall),
            WallName = wall.Name,
            DurationSeconds = ParsePositiveInt(ResistanceDurationSecondsEntry.Text, "La durata della resistenza deve essere un numero positivo."),
            ActiveMode = (ResistanceModePicker.SelectedItem as string) ?? "steady",
            BlinkPeriodMs = ParseRangeInt(ResistanceBlinkPeriodMsEntry.Text, 100, 5000, "Il periodo feedback deve essere tra 100 e 5000 ms."),
            CompletedHoldSeconds = ParseRangeInt(CompletedHoldSecondsEntry.Text, 0, 30, "La durata verde finale deve essere tra 0 e 30 secondi."),
            ActiveColor = ParseHexColor(ResistanceActiveColorValueLabel.Text, "Il colore attivita deve essere in formato #RRGGBB."),
            CompletedColor = ParseHexColor(ResistanceCompletedColorValueLabel.Text, "Il colore fine resistenza deve essere in formato #RRGGBB.")
        };
    }

    private HangStepDefinition BuildHangDefinitionFromEditor()
    {
        var wall = selectedWall ?? throw new InvalidOperationException("Seleziona prima una parete.");
        if (selectedHangHoleNumbers.Count is < 1 or > 2)
        {
            throw new InvalidOperationException("Seleziona 1 o 2 prese per la sospensione.");
        }

        return new HangStepDefinition
        {
            Name = "Sospensione",
            RoomName = wall.RoomName,
            WallId = Esp32PayloadBuilderService.BuildWallId(wall),
            WallName = wall.Name,
            DurationSeconds = ParsePositiveInt(HangDurationSecondsEntry.Text, "La durata della sospensione deve essere un numero positivo."),
            CompletedHoldSeconds = ParseRangeInt(HangCompletedHoldSecondsEntry.Text, 0, 30, "La durata verde finale deve essere tra 0 e 30 secondi."),
            ActiveColor = ParseHexColor(HangActiveColorValueLabel.Text, "Il colore sospensione deve essere in formato #RRGGBB."),
            CompletedColor = ParseHexColor(HangCompletedColorValueLabel.Text, "Il colore fine sospensione deve essere in formato #RRGGBB."),
            TargetHoleNumbers = selectedHangHoleNumbers.OrderBy(value => value).ToList()
        };
    }

    private WorkoutStepDefinition BuildWorkoutStepFromEditor(string? stepId = null)
    {
        if (WorkoutStepTypePicker.SelectedItem is not WorkoutStepType stepType)
        {
            throw new InvalidOperationException("Seleziona prima il tipo step.");
        }

        var stepName = ReadRequiredText(WorkoutStepNameEntry.Text, "Inserisci un nome step valido.");
        var workSeconds = ParsePositiveInt(WorkoutStepWorkSecondsEntry.Text, "Il tempo lavoro dello step deve essere un numero positivo.");
        var initialRestSeconds = ParseRangeInt(WorkoutStepInitialRestSecondsEntry.Text, 0, 3600, "Il rest iniziale deve essere tra 0 e 3600 secondi.");
        var finalRestSeconds = ParseRangeInt(WorkoutStepFinalRestSecondsEntry.Text, 0, 3600, "Il rest finale deve essere tra 0 e 3600 secondi.");
        var repetitions = ParseRangeInt(WorkoutStepRepetitionsEntry.Text, 1, 999, "Le ripetizioni devono essere almeno 1.");

        return stepType switch
        {
            WorkoutStepType.Rest => new WorkoutStepDefinition
            {
                StepId = string.IsNullOrWhiteSpace(stepId) ? Guid.NewGuid().ToString("N") : stepId,
                StepType = stepType,
                Name = stepName,
                WorkSeconds = workSeconds,
                InitialRestSeconds = initialRestSeconds,
                FinalRestSeconds = finalRestSeconds,
                Repetitions = repetitions,
                RestPayload = new WorkoutRestStepPayload
                {
                    BlinkColor = BlinkColorValueLabel.Text,
                    CompletedColor = CompletedColorValueLabel.Text,
                    BlinkPeriodMs = ParseRangeInt(BlinkPeriodMsEntry.Text, 100, 5000, "Il blink del recupero deve essere tra 100 e 5000 ms."),
                    CompletedHoldSeconds = ParseRangeInt(CompletedHoldSecondsEntry.Text, 0, 30, "Il verde finale del recupero deve essere tra 0 e 30 secondi.")
                }
            },
            WorkoutStepType.Resistance => new WorkoutStepDefinition
            {
                StepId = string.IsNullOrWhiteSpace(stepId) ? Guid.NewGuid().ToString("N") : stepId,
                StepType = stepType,
                Name = stepName,
                WorkSeconds = workSeconds,
                InitialRestSeconds = initialRestSeconds,
                FinalRestSeconds = finalRestSeconds,
                Repetitions = repetitions,
                ResistancePayload = new WorkoutResistanceStepPayload
                {
                    ActiveMode = (ResistanceModePicker.SelectedItem as string) ?? "steady",
                    ActiveColor = ResistanceActiveColorValueLabel.Text,
                    CompletedColor = ResistanceCompletedColorValueLabel.Text,
                    BlinkPeriodMs = ParseRangeInt(ResistanceBlinkPeriodMsEntry.Text, 100, 5000, "Il periodo resistenza deve essere tra 100 e 5000 ms."),
                    CompletedHoldSeconds = ParseRangeInt(CompletedHoldSecondsEntry.Text, 0, 30, "Il verde finale della resistenza deve essere tra 0 e 30 secondi.")
                }
            },
            WorkoutStepType.Hang => new WorkoutStepDefinition
            {
                StepId = string.IsNullOrWhiteSpace(stepId) ? Guid.NewGuid().ToString("N") : stepId,
                StepType = stepType,
                Name = stepName,
                WorkSeconds = workSeconds,
                InitialRestSeconds = initialRestSeconds,
                FinalRestSeconds = finalRestSeconds,
                Repetitions = repetitions,
                HangPayload = new WorkoutHangStepPayload
                {
                    TargetHoleNumbers = selectedHangHoleNumbers.OrderBy(value => value).ToList(),
                    ActiveColor = HangActiveColorValueLabel.Text,
                    CompletedColor = HangCompletedColorValueLabel.Text,
                    CompletedHoldSeconds = ParseRangeInt(HangCompletedHoldSecondsEntry.Text, 0, 30, "Il verde finale della sospensione deve essere tra 0 e 30 secondi.")
                }
            },
            WorkoutStepType.Circuit => new WorkoutStepDefinition
            {
                StepId = string.IsNullOrWhiteSpace(stepId) ? Guid.NewGuid().ToString("N") : stepId,
                StepType = stepType,
                Name = stepName,
                WorkSeconds = workSeconds,
                InitialRestSeconds = initialRestSeconds,
                FinalRestSeconds = finalRestSeconds,
                Repetitions = repetitions,
                CircuitPayload = BuildWorkoutCircuitPayload()
            },
            _ => throw new InvalidOperationException("Tipo step non supportato.")
        };
    }

    private WorkoutCircuitStepPayload BuildWorkoutCircuitPayload()
    {
        if (WorkoutCircuitPicker.SelectedItem is not CircuitDefinition circuit)
        {
            throw new InvalidOperationException("Seleziona un circuito per lo step circuito.");
        }

        return new WorkoutCircuitStepPayload
        {
            CircuitId = Esp32PayloadBuilderService.BuildCircuitId(circuit),
            CircuitName = circuit.Name,
            Mode = WorkoutCircuitModePicker.SelectedItem is WorkoutCircuitMode mode
                ? mode
                : WorkoutCircuitMode.Start
        };
    }

    private WorkoutDefinition BuildWorkoutDefinitionFromEditor()
    {
        var wall = selectedWall ?? throw new InvalidOperationException("Seleziona prima una parete.");
        return new WorkoutDefinition
        {
            WorkoutId = string.IsNullOrWhiteSpace(currentWorkoutId) ? Guid.NewGuid().ToString("N") : currentWorkoutId,
            Name = ReadRequiredText(WorkoutNameEntry.Text, "Inserisci un nome allenamento valido."),
            Description = WorkoutDescriptionEntry.Text?.Trim() ?? string.Empty,
            RoomName = wall.RoomName,
            WallId = Esp32PayloadBuilderService.BuildWallId(wall),
            WallName = wall.Name,
            Steps = workoutSteps.ToList()
        };
    }

    private void ApplyRestRuntimeState(RestExecutionState state)
    {
        var effectiveRemaining = state.Status == RestExecutionStatus.Idle
            ? ParsePositiveIntOrDefault(DurationSecondsEntry.Text, 60)
            : Math.Max(0, state.RemainingSeconds);

        CountdownLabel.Text = TimeSpan.FromSeconds(effectiveRemaining).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        RuntimeStatusLabel.Text = state.StatusMessage;
        LedFeedbackStatusLabel.Text = state.LedFeedbackMessage;

        StartButton.IsEnabled = selectedWall is not null && state.Status is RestExecutionStatus.Idle or RestExecutionStatus.Completed or RestExecutionStatus.Cancelled;
        PauseButton.IsEnabled = state.Status == RestExecutionStatus.Running;
        ResumeButton.IsEnabled = state.Status == RestExecutionStatus.Paused;
        StopButton.IsEnabled = state.Status is RestExecutionStatus.Running or RestExecutionStatus.Paused or RestExecutionStatus.Completed;
    }

    private void ApplyResistanceRuntimeState(ResistanceExecutionState state)
    {
        var effectiveRemaining = state.Status == ResistanceExecutionStatus.Idle
            ? ParsePositiveIntOrDefault(ResistanceDurationSecondsEntry.Text, 120)
            : Math.Max(0, state.RemainingSeconds);

        ResistanceCountdownLabel.Text = TimeSpan.FromSeconds(effectiveRemaining).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        ResistanceRuntimeStatusLabel.Text = state.StatusMessage;
        ResistanceLedFeedbackStatusLabel.Text = state.LedFeedbackMessage;

        ResistanceStartButton.IsEnabled = selectedWall is not null && state.Status is ResistanceExecutionStatus.Idle or ResistanceExecutionStatus.Completed or ResistanceExecutionStatus.Cancelled;
        ResistancePauseButton.IsEnabled = state.Status == ResistanceExecutionStatus.Running;
        ResistanceResumeButton.IsEnabled = state.Status == ResistanceExecutionStatus.Paused;
        ResistanceStopButton.IsEnabled = state.Status is ResistanceExecutionStatus.Running or ResistanceExecutionStatus.Paused or ResistanceExecutionStatus.Completed;
    }

    private void ApplyHangRuntimeState(HangExecutionState state)
    {
        var effectiveRemaining = state.Status == HangExecutionStatus.Idle
            ? ParsePositiveIntOrDefault(HangDurationSecondsEntry.Text, 10)
            : Math.Max(0, state.RemainingSeconds);

        HangCountdownLabel.Text = TimeSpan.FromSeconds(effectiveRemaining).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        HangRuntimeStatusLabel.Text = state.StatusMessage;
        HangLedFeedbackStatusLabel.Text = state.LedFeedbackMessage;

        HangStartButton.IsEnabled = selectedWall is not null &&
                                    selectedHangHoleNumbers.Count is > 0 and <= 2 &&
                                    state.Status is HangExecutionStatus.Idle or HangExecutionStatus.Completed or HangExecutionStatus.Cancelled;
        HangPauseButton.IsEnabled = state.Status == HangExecutionStatus.Running;
        HangResumeButton.IsEnabled = state.Status == HangExecutionStatus.Paused;
        HangStopButton.IsEnabled = state.Status is HangExecutionStatus.Running or HangExecutionStatus.Paused or HangExecutionStatus.Completed;
    }

    private void ApplyWorkoutRuntimeState(WorkoutExecutionState state)
    {
        var editorName = string.IsNullOrWhiteSpace(WorkoutNameEntry.Text) ? "Allenamento" : WorkoutNameEntry.Text!.Trim();
        WorkoutRuntimeNameLabel.Text = state.Status == WorkoutExecutionStatus.Idle
            ? $"Editor attivo: {editorName}"
            : $"Allenamento attivo: {editorName}";
        WorkoutRuntimeCountdownLabel.Text = TimeSpan.FromSeconds(Math.Max(0, state.RemainingSeconds)).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        WorkoutRuntimeStatusLabel.Text = state.StatusMessage;
        WorkoutRuntimeStepLabel.Text = state.CurrentPlanIndex >= 0
            ? $"Fase {state.CurrentPlanIndex + 1}/{Math.Max(1, state.TotalPhases)} - Step {state.CurrentStepIndex + 1}/{Math.Max(1, state.TotalSteps)} - {state.CurrentStepName}"
            : "Compila o carica un allenamento.";

        WorkoutStartButton.IsEnabled = selectedWall is not null &&
                                      state.Status is WorkoutExecutionStatus.Idle or WorkoutExecutionStatus.Completed or WorkoutExecutionStatus.Cancelled;
        WorkoutPauseButton.IsEnabled = state.Status == WorkoutExecutionStatus.Running;
        WorkoutResumeButton.IsEnabled = state.Status == WorkoutExecutionStatus.Paused;
        WorkoutStopButton.IsEnabled = state.Status is WorkoutExecutionStatus.Running or WorkoutExecutionStatus.Paused or WorkoutExecutionStatus.Completed;
    }

    private void RefreshHangPreview()
    {
        hangPreviewDrawable.Wall = selectedWall;
        hangPreviewDrawable.Circuit = null;
        hangPreviewDrawable.HighlightedHole = highlightedHangHole;
        hangPreviewDrawable.SelectedHoles = GetSelectedHangHoles();
        UpdateHangPreviewScale();
        HangPreviewCanvas.Invalidate();
    }

    private IReadOnlyList<WallHoleDefinition> GetSelectedHangHoles()
    {
        if (selectedWall is null || selectedHangHoleNumbers.Count == 0)
        {
            return Array.Empty<WallHoleDefinition>();
        }

        var holes = selectedWall.GetOrderedHoles();
        return selectedHangHoleNumbers
            .Select(number => holes.FirstOrDefault(hole => hole.Number == number))
            .Where(hole => hole.Number > 0)
            .ToList();
    }

    private void UpdateHangSelectionLabel()
    {
        HangSelectionLabel.Text = selectedHangHoleNumbers.Count == 0
            ? "Nessuna presa selezionata."
            : $"Prese selezionate: {string.Join(", ", selectedHangHoleNumbers.OrderBy(value => value))}";
    }

    private void UpdateHangPreviewScale()
    {
        var wall = selectedWall;
        if (wall is null || wall.Width <= 0 || wall.Height <= 0)
        {
            hangPreviewDrawable.PixelsPerMillimeter = 0.1f;
            return;
        }

        const double padding = 48d;
        var availableWidth = Math.Max(1d, hangPreviewBaseWidth - padding);
        var availableHeight = Math.Max(1d, hangPreviewBaseHeight - padding);
        var fitScale = Math.Min(availableWidth / wall.Width, availableHeight / wall.Height);
        hangPreviewDrawable.PixelsPerMillimeter = (float)Math.Max(0.01d, fitScale);
        hangPreviewDrawable.ZoomFactor = 1f;
    }

    private void RefreshRestColorPreviews()
    {
        ApplyColorPreview(BlinkColorPreview, BlinkColorValueLabel.Text);
        ApplyColorPreview(CompletedColorPreview, CompletedColorValueLabel.Text);
    }

    private void RefreshResistanceColorPreviews()
    {
        ApplyColorPreview(ResistanceActiveColorPreview, ResistanceActiveColorValueLabel.Text);
        ApplyColorPreview(ResistanceCompletedColorPreview, ResistanceCompletedColorValueLabel.Text);
    }

    private void RefreshHangColorPreviews()
    {
        ApplyColorPreview(HangActiveColorPreview, HangActiveColorValueLabel.Text);
        ApplyColorPreview(HangCompletedColorPreview, HangCompletedColorValueLabel.Text);
    }

    private void RefreshWorkoutPayloadSummaries()
    {
        WorkoutRestPayloadSummaryLabel.Text =
            $"Recupero: blink {BlinkColorValueLabel.Text}, fine {CompletedColorValueLabel.Text}, periodo {BlinkPeriodMsEntry.Text ?? "500"} ms.";
        WorkoutResistancePayloadSummaryLabel.Text =
            $"Resistenza: mode {(ResistanceModePicker.SelectedItem as string ?? "steady")}, attivo {ResistanceActiveColorValueLabel.Text}, fine {ResistanceCompletedColorValueLabel.Text}.";
        WorkoutHangPayloadSummaryLabel.Text =
            selectedHangHoleNumbers.Count == 0
                ? "Sospensione: seleziona 1 o 2 prese sulla parete."
                : $"Sospensione: prese {string.Join(", ", selectedHangHoleNumbers.OrderBy(value => value))}, attivo {HangActiveColorValueLabel.Text}, fine {HangCompletedColorValueLabel.Text}.";
    }

    private void UpdateWorkoutStepBuilderVisibility()
    {
        var selectedType = WorkoutStepTypePicker.SelectedItem is WorkoutStepType type
            ? type
            : WorkoutStepType.Rest;
        WorkoutRestPayloadHost.IsVisible = selectedType == WorkoutStepType.Rest;
        WorkoutResistancePayloadHost.IsVisible = selectedType == WorkoutStepType.Resistance;
        WorkoutHangPayloadHost.IsVisible = selectedType == WorkoutStepType.Hang;
        WorkoutCircuitPayloadHost.IsVisible = selectedType == WorkoutStepType.Circuit;
    }

    private void RebuildWorkoutStepsList()
    {
        WorkoutStepsHost.Children.Clear();
        WorkoutStepsEmptyLabel.IsVisible = workoutSteps.Count == 0;

        for (var index = 0; index < workoutSteps.Count; index++)
        {
            var step = workoutSteps[index];
            var isSelected = index == selectedWorkoutStepIndex;
            var border = new Border
            {
                Background = isSelected ? Color.FromArgb("#2A2212") : Color.FromArgb("#191611"),
                Stroke = isSelected ? Color.FromArgb("#F2C94C") : GetStepAccentColor(step.StepType),
                StrokeThickness = isSelected ? 3 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = 12
            };

            var headerRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            headerRow.Add(new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = $"{index + 1}. {step.Name}",
                        FontSize = 16,
                        TextColor = Color.FromArgb("#F8E7A8"),
                        FontFamily = "OpenSansSemibold"
                    },
                    new Label
                    {
                        Text = BuildStepTimingSummary(step),
                        FontSize = 12,
                        TextColor = Color.FromArgb("#D8A72D")
                    }
                }
            });

            headerRow.Add(CreatePillLabel(GetStepTypeLabel(step.StepType), GetStepAccentColor(step.StepType)), 1, 0);

            border.Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    headerRow,
                    new Label
                    {
                        Text = BuildStepPayloadSummary(step),
                        FontSize = 12,
                        TextColor = Color.FromArgb("#B9AA79")
                    },
                    new Label
                    {
                        Text = isSelected ? "Selezionato per modifica e riordino." : "Tocca per modificare questo step.",
                        FontSize = 11,
                        TextColor = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#8E7531")
                    }
                }
            };

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    selectedWorkoutStepIndex = index;
                    WorkoutEditorStatusLabel.Text = $"Step selezionato: {step.Name}";
                    LoadSelectedStepIntoBuilder();
                    RebuildWorkoutStepsList();
                })
            });

            WorkoutStepsHost.Children.Add(border);
        }

        RefreshWorkoutEditorState();
    }

    private void RebuildSavedWorkoutsList(IReadOnlyList<WorkoutDefinition> visibleWorkouts)
    {
        SavedWorkoutsHost.Children.Clear();
        SavedWorkoutsEmptyLabel.IsVisible = visibleWorkouts.Count == 0;

        foreach (var workout in visibleWorkouts)
        {
            var isSelected = string.Equals(workout.WorkoutId, currentWorkoutId, StringComparison.Ordinal);
            var border = new Border
            {
                Background = isSelected ? Color.FromArgb("#2A2212") : Color.FromArgb("#191611"),
                Stroke = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#8E7531"),
                StrokeThickness = isSelected ? 3 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = 12
            };

            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            header.Add(new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label
                    {
                        Text = workout.Name,
                        FontSize = 15,
                        FontFamily = "OpenSansSemibold",
                        TextColor = Color.FromArgb("#F8E7A8")
                    },
                    new Label
                    {
                        Text = $"{workout.Steps.Count} step Â· {BuildWorkoutDurationSummary(workout)}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#D8A72D")
                    }
                }
            });

            header.Add(CreatePillLabel(isSelected ? "Attivo" : "Salvato", isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#8E7531")), 1, 0);

            border.Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    header,
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(workout.Description) ? "Nessuna descrizione." : workout.Description,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#B9AA79")
                    },
                    new Label
                    {
                        Text = BuildWorkoutStepTypeSummary(workout),
                        FontSize = 11,
                        TextColor = Color.FromArgb("#8E7531")
                    }
                }
            };

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    isLoadingSavedWorkout = true;
                    SavedWorkoutPicker.SelectedItem = workout;
                    isLoadingSavedWorkout = false;
                    LoadWorkoutIntoEditor(workout);
                    RefreshView();
                })
            });

            SavedWorkoutsHost.Children.Add(border);
        }
    }

    private void MoveSelectedWorkoutStep(int direction)
    {
        if (selectedWorkoutStepIndex < 0 || selectedWorkoutStepIndex >= workoutSteps.Count)
        {
            WorkoutEditorStatusLabel.Text = "Seleziona prima uno step da spostare.";
            return;
        }

        var targetIndex = selectedWorkoutStepIndex + direction;
        if (targetIndex < 0 || targetIndex >= workoutSteps.Count)
        {
            return;
        }

        (workoutSteps[selectedWorkoutStepIndex], workoutSteps[targetIndex]) = (workoutSteps[targetIndex], workoutSteps[selectedWorkoutStepIndex]);
        selectedWorkoutStepIndex = targetIndex;
        WorkoutEditorStatusLabel.Text = $"Step spostato: {workoutSteps[selectedWorkoutStepIndex].Name}";
        RebuildWorkoutStepsList();
        RefreshWorkoutPlanPreview();
    }

    private void RefreshWorkoutPlanPreview()
    {
        WorkoutPlanHost.Children.Clear();

        try
        {
            var workout = BuildWorkoutDefinitionFromEditor();
            if (workout.Steps.Count == 0)
            {
                WorkoutPlanStatusLabel.Text = "Aggiungi almeno uno step per vedere il piano runtime.";
                return;
            }

            var plan = app.WorkoutExecutionService.BuildExecutionPlan(workout);
            WorkoutPlanStatusLabel.Text = $"Fasi totali: {plan.Count} - Step: {workout.Steps.Count}";

            foreach (var phase in plan)
            {
                var phaseBorder = new Border
                {
                    Background = Color.FromArgb("#191611"),
                    Stroke = phase.Phase == WorkoutRuntimePhase.Work
                        ? GetStepAccentColor(phase.StepType)
                        : Color.FromArgb("#8E7531"),
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = 10
                };

                var phaseHeader = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    }
                };

                phaseHeader.Add(new VerticalStackLayout
                {
                    Spacing = 3,
                    Children =
                    {
                        new Label
                        {
                            Text = $"{phase.PlanIndex + 1}. {phase.StepName}",
                            FontSize = 13,
                            FontFamily = "OpenSansSemibold",
                            TextColor = Color.FromArgb("#F8E7A8")
                        },
                        new Label
                        {
                            Text = $"{GetStepTypeLabel(phase.StepType)} Â· rip {phase.Repetition}/{phase.TotalRepetitions}",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#D8A72D")
                        }
                    }
                });

                phaseHeader.Add(CreatePillLabel(
                    GetRuntimePhaseLabel(phase.Phase),
                    phase.Phase == WorkoutRuntimePhase.Work ? GetStepAccentColor(phase.StepType) : Color.FromArgb("#8E7531")), 1, 0);

                phaseBorder.Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        phaseHeader,
                        new Label
                        {
                            Text = $"Durata {phase.DurationSeconds}s",
                            FontSize = 12,
                            TextColor = Color.FromArgb("#B9AA79")
                        }
                    }
                };

                WorkoutPlanHost.Children.Add(phaseBorder);
            }
        }
        catch (Exception ex)
        {
            WorkoutPlanStatusLabel.Text = $"Piano non disponibile: {ex.Message}";
        }
    }

    private static string BuildStepTimingSummary(WorkoutStepDefinition step)
    {
        return $"Work {step.WorkSeconds}s Â· Rest iniziale {step.InitialRestSeconds}s Â· Rest finale {step.FinalRestSeconds}s Â· Rip {step.Repetitions}";
    }

    private static string BuildStepPayloadSummary(WorkoutStepDefinition step)
    {
        return step.StepType switch
        {
            WorkoutStepType.Rest => step.RestPayload is null
                ? "Recupero con parametri di default."
                : $"Blink {step.RestPayload.BlinkColor ?? "#FF0000"} Â· fine {step.RestPayload.CompletedColor ?? "#00FF00"} Â· periodo {step.RestPayload.BlinkPeriodMs ?? 500} ms",
            WorkoutStepType.Resistance => step.ResistancePayload is null
                ? "Resistenza libera con parametri di default."
                : $"{step.ResistancePayload.ActiveMode} Â· attivo {step.ResistancePayload.ActiveColor ?? "#FF8C00"} Â· fine {step.ResistancePayload.CompletedColor ?? "#00FF00"}",
            WorkoutStepType.Hang => step.HangPayload is null
                ? "Sospensione con target non definiti."
                : $"Prese {string.Join(", ", step.HangPayload.TargetHoleNumbers)} Â· attivo {step.HangPayload.ActiveColor ?? "#00BFFF"} Â· fine {step.HangPayload.CompletedColor ?? "#00FF00"}",
            WorkoutStepType.Circuit => step.CircuitPayload is null
                ? "Circuito non definito."
                : $"{step.CircuitPayload.CircuitName} Â· modalita {step.CircuitPayload.Mode}",
            _ => string.Empty
        };
    }

    private static string GetStepTypeLabel(WorkoutStepType stepType)
    {
        return stepType switch
        {
            WorkoutStepType.Rest => "Recupero",
            WorkoutStepType.Resistance => "Resistenza",
            WorkoutStepType.Hang => "Sospensione",
            WorkoutStepType.Circuit => "Circuito",
            _ => stepType.ToString()
        };
    }

    private static string GetRuntimePhaseLabel(WorkoutRuntimePhase phase)
    {
        return phase switch
        {
            WorkoutRuntimePhase.InitialRest => "Rest iniziale",
            WorkoutRuntimePhase.Work => "Lavoro",
            WorkoutRuntimePhase.FinalRest => "Rest finale",
            _ => phase.ToString()
        };
    }

    private static Color GetStepAccentColor(WorkoutStepType stepType)
    {
        return stepType switch
        {
            WorkoutStepType.Rest => Color.FromArgb("#D97B29"),
            WorkoutStepType.Resistance => Color.FromArgb("#C44536"),
            WorkoutStepType.Hang => Color.FromArgb("#247BA0"),
            WorkoutStepType.Circuit => Color.FromArgb("#6C9A2B"),
            _ => Color.FromArgb("#B9922F")
        };
    }

    private static Border CreatePillLabel(string text, Color strokeColor)
    {
        return new Border
        {
            Background = Color.FromArgb("#241F17"),
            Stroke = strokeColor,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(10, 4),
            Content = new Label
            {
                Text = text,
                FontSize = 11,
                TextColor = strokeColor,
                FontFamily = "OpenSansSemibold",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    private static string BuildWorkoutDurationSummary(WorkoutDefinition workout)
    {
        var totalSeconds = workout.Steps.Sum(step =>
            (step.WorkSeconds + step.InitialRestSeconds + step.FinalRestSeconds) * Math.Max(1, step.Repetitions));
        return $"totale {totalSeconds}s";
    }

    private static string BuildWorkoutStepTypeSummary(WorkoutDefinition workout)
    {
        var labels = workout.Steps
            .Select(step => GetStepTypeLabel(step.StepType))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return labels.Count == 0 ? "Nessun tipo step." : string.Join(" Â· ", labels);
    }

    private void LoadWorkoutIntoEditor(WorkoutDefinition workout)
    {
        currentWorkoutId = workout.WorkoutId;
        WorkoutNameEntry.Text = workout.Name;
        WorkoutDescriptionEntry.Text = workout.Description;
        workoutSteps.Clear();
        workoutSteps.AddRange(workout.Steps);
        selectedWorkoutStepIndex = workoutSteps.Count == 0 ? -1 : 0;
        LoadSelectedStepIntoBuilder();
        WorkoutEditorStatusLabel.Text = $"Allenamento caricato: {workout.Name}";
        RefreshWorkoutEditorState();
        RebuildWorkoutStepsList();
        RefreshWorkoutPlanPreview();
    }

    private void ResetWorkoutEditor()
    {
        currentWorkoutId = null;
        WorkoutNameEntry.Text = "Allenamento A";
        WorkoutDescriptionEntry.Text = string.Empty;
        workoutSteps.Clear();
        selectedWorkoutStepIndex = -1;
        ResetWorkoutStepBuilder();
        WorkoutEditorStatusLabel.Text = "Editor reset. Crea un nuovo allenamento.";
        RefreshWorkoutEditorState();
        RebuildWorkoutStepsList();
        RefreshWorkoutPlanPreview();
    }

    private async Task<WorkoutDefinition> SaveCurrentWorkoutAsync(CancellationToken cancellationToken = default)
    {
        var workout = BuildWorkoutDefinitionFromEditor();
        await app.WorkoutRepository.SaveAsync(workout, cancellationToken);
        currentWorkoutId = workout.WorkoutId;
        availableWorkouts = (await app.WorkoutRepository.GetAllAsync(cancellationToken)).ToList();
        RefreshView();
        return workout;
    }

    private void LoadSelectedStepIntoBuilder()
    {
        if (selectedWorkoutStepIndex < 0 || selectedWorkoutStepIndex >= workoutSteps.Count)
        {
            return;
        }

        var step = workoutSteps[selectedWorkoutStepIndex];
        WorkoutStepTypePicker.SelectedItem = step.StepType;
        WorkoutStepNameEntry.Text = step.Name;
        WorkoutStepWorkSecondsEntry.Text = step.WorkSeconds.ToString(CultureInfo.InvariantCulture);
        WorkoutStepInitialRestSecondsEntry.Text = step.InitialRestSeconds.ToString(CultureInfo.InvariantCulture);
        WorkoutStepFinalRestSecondsEntry.Text = step.FinalRestSeconds.ToString(CultureInfo.InvariantCulture);
        WorkoutStepRepetitionsEntry.Text = step.Repetitions.ToString(CultureInfo.InvariantCulture);

        switch (step.StepType)
        {
            case WorkoutStepType.Rest when step.RestPayload is not null:
                BlinkPeriodMsEntry.Text = (step.RestPayload.BlinkPeriodMs ?? 500).ToString(CultureInfo.InvariantCulture);
                CompletedHoldSecondsEntry.Text = (step.RestPayload.CompletedHoldSeconds ?? 3).ToString(CultureInfo.InvariantCulture);
                BlinkColorValueLabel.Text = step.RestPayload.BlinkColor ?? "#FF0000";
                CompletedColorValueLabel.Text = step.RestPayload.CompletedColor ?? "#00FF00";
                RefreshRestColorPreviews();
                break;
            case WorkoutStepType.Resistance when step.ResistancePayload is not null:
                ResistanceModePicker.SelectedItem = step.ResistancePayload.ActiveMode;
                ResistanceBlinkPeriodMsEntry.Text = (step.ResistancePayload.BlinkPeriodMs ?? 1000).ToString(CultureInfo.InvariantCulture);
                ResistanceActiveColorValueLabel.Text = step.ResistancePayload.ActiveColor ?? "#FF8C00";
                ResistanceCompletedColorValueLabel.Text = step.ResistancePayload.CompletedColor ?? "#00FF00";
                RefreshResistanceColorPreviews();
                break;
            case WorkoutStepType.Hang when step.HangPayload is not null:
                HangCompletedHoldSecondsEntry.Text = (step.HangPayload.CompletedHoldSeconds ?? 3).ToString(CultureInfo.InvariantCulture);
                HangActiveColorValueLabel.Text = step.HangPayload.ActiveColor ?? "#00BFFF";
                HangCompletedColorValueLabel.Text = step.HangPayload.CompletedColor ?? "#00FF00";
                RefreshHangColorPreviews();
                selectedHangHoleNumbers.Clear();
                selectedHangHoleNumbers.AddRange(step.HangPayload.TargetHoleNumbers.OrderBy(value => value));
                RefreshHangPreview();
                UpdateHangSelectionLabel();
                break;
            case WorkoutStepType.Circuit when step.CircuitPayload is not null:
                WorkoutCircuitModePicker.SelectedItem = step.CircuitPayload.Mode;
                WorkoutCircuitPicker.SelectedItem = GetVisibleCircuits().FirstOrDefault(circuit =>
                    string.Equals(Esp32PayloadBuilderService.BuildCircuitId(circuit), step.CircuitPayload.CircuitId, StringComparison.Ordinal));
                break;
            default:
                selectedHangHoleNumbers.Clear();
                RefreshHangPreview();
                UpdateHangSelectionLabel();
                break;
        }

        UpdateWorkoutStepBuilderVisibility();
        RefreshWorkoutPayloadSummaries();
        RefreshWorkoutEditorState();
    }

    private void ResetWorkoutStepBuilder()
    {
        WorkoutStepTypePicker.SelectedItem ??= WorkoutStepType.Rest;
        WorkoutStepNameEntry.Text = "Step 1";
        WorkoutStepWorkSecondsEntry.Text = "20";
        WorkoutStepInitialRestSecondsEntry.Text = "0";
        WorkoutStepFinalRestSecondsEntry.Text = "0";
        WorkoutStepRepetitionsEntry.Text = "1";
        selectedHangHoleNumbers.Clear();
        RefreshHangPreview();
        UpdateHangSelectionLabel();
        RefreshWorkoutEditorState();
    }

    private void RefreshWorkoutEditorState()
    {
        var hasSavedWorkout = !string.IsNullOrWhiteSpace(currentWorkoutId);
        var hasSelectedStep = selectedWorkoutStepIndex >= 0 && selectedWorkoutStepIndex < workoutSteps.Count;
        var hasAnySteps = workoutSteps.Count > 0;

        WorkoutEditorModeLabel.Text = hasSavedWorkout
            ? $"Stai modificando un allenamento salvato: {WorkoutNameEntry.Text?.Trim()}"
            : "Stai creando un nuovo allenamento.";

        WorkoutSelectedStepLabel.Text = hasSelectedStep
            ? $"Step selezionato: {selectedWorkoutStepIndex + 1}/{workoutSteps.Count} - {workoutSteps[selectedWorkoutStepIndex].Name}"
            : hasAnySteps
                ? "Tocca uno step nella lista per modificarlo o riordinarlo."
                : "Nessuno step selezionato.";

        SaveWorkoutButton.Text = hasSavedWorkout ? "Aggiorna allenamento" : "Salva allenamento";
        DeleteWorkoutButton.IsEnabled = SavedWorkoutPicker.SelectedItem is WorkoutDefinition;
        UpdateWorkoutStepButton.IsEnabled = hasSelectedStep;
        RemoveWorkoutStepButton.IsEnabled = hasSelectedStep;
        MoveWorkoutStepUpButton.IsEnabled = hasSelectedStep && selectedWorkoutStepIndex > 0;
        MoveWorkoutStepDownButton.IsEnabled = hasSelectedStep && selectedWorkoutStepIndex >= 0 && selectedWorkoutStepIndex < workoutSteps.Count - 1;
    }

    private static void ApplyColorPreview(BoxView preview, string? text)
    {
        preview.Color = TryParseColor(text, out var color)
            ? color
            : Color.FromArgb("#3A3120");
    }

    private static string ReadRequiredText(string? text, string errorMessage)
    {
        var value = text?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static int ParsePositiveInt(string? text, string errorMessage)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static int ParsePositiveIntOrDefault(string? text, int fallback)
    {
        return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }

    private static int ParseRangeInt(string? text, int min, int max, string errorMessage)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value >= min &&
            value <= max)
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static string ParseHexColor(string? text, string errorMessage)
    {
        var value = text?.Trim().ToUpperInvariant();
        if (TryParseColor(value, out _) && value is not null && value.Length == 7)
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static bool TryParseColor(string? text, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            color = Color.FromArgb(text.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ToHexColor(Color color)
    {
        var red = (byte)Math.Round(color.Red * 255d);
        var green = (byte)Math.Round(color.Green * 255d);
        var blue = (byte)Math.Round(color.Blue * 255d);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }
}

