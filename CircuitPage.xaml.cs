using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using System.Globalization;
using RuoteLab.Drawing;
using RuoteLab.Models;
using RuoteLab.Services;
using RuoteLab.ViewModels;

namespace RuoteLab;

public partial class CircuitPage : ContentPage
{
    private enum CircuitColorTarget
    {
        RightHand,
        LeftHand,
        Start,
        Top
    }

    private readonly ICircuitPageStateService pageStateService;
    private readonly INextHoldSuggestionService nextHoldSuggestionService;
    private readonly CircuitEditorViewModel viewModel;
    private readonly CircuitEditorDrawable previewDrawable = new();
    private double previewZoom = 1d;
    private double previewZoomStart = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;
    private CircuitInteractionMode interactionMode = CircuitInteractionMode.Select;
    private HandSide specialModeHand = HandSide.Right;
    private WallHoleDefinition? highlightedHole;
    private WallHoleDefinition? currentLeftFootStateHole;
    private WallHoleDefinition? currentRightFootStateHole;
    private NextHoldSuggestionResult? lastSuggestionResult;
    private bool isRefreshing;
    private bool isCircuitGlobalsExpanded = false;
    private CircuitColorTarget activeCircuitColorTarget = CircuitColorTarget.RightHand;
    private bool isUpdatingCircuitColorControls;

    public CircuitPage()
    {
        try
        {
            InitializeComponent();

            var app = (App)Application.Current!;
            pageStateService = app.CircuitPageStateService;
            nextHoldSuggestionService = app.NextHoldSuggestionService;
            viewModel = app.CircuitEditorViewModel;
            CircuitPreviewCanvas.Drawable = previewDrawable;
            CircuitRoomPicker.ItemsSource = viewModel.GetAvailableRooms().ToList();
            CircuitWallPicker.ItemsSource = viewModel.GetWallsForSelectedRoom().ToList();
            CircuitDifficultyPicker.ItemsSource = ClimbingGradeScale.OrderedGrades.ToList();

            CircuitNameEntry.Text = viewModel.SuggestedCircuitName;
            ApplyCircuitGlobalsEditorExpansion();
            SyncView();
        }
        catch (Exception ex)
        {
            var databaseFactory = new Persistence.SqliteDatabaseFactory();
            var wallRepository = new Services.SqliteWallRepository(databaseFactory);
            var roomRepository = new Services.SqliteRoomRepository(databaseFactory);
            pageStateService = new Services.CircuitPageStateService();
            nextHoldSuggestionService = new Services.NextHoldSuggestionService();
            viewModel = new ViewModels.CircuitEditorViewModel(
                new Services.CircuitEditingService(),
                new ViewModels.GymSetupViewModel(
                    new Services.GymSetupService(),
                    new Services.WallConfigurationStorageService(wallRepository),
                    wallRepository,
                    roomRepository),
                new Services.SqliteCircuitRepository(databaseFactory));
            CircuitDifficultyPicker.ItemsSource = ClimbingGradeScale.OrderedGrades.ToList();
            Title = "Errore Circuiti";
            Content = BuildErrorView("Errore inizializzazione CircuitPage", ex);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (isRefreshing)
        {
            return;
        }

        try
        {
            isRefreshing = true;
            await viewModel.LoadCircuitsAsync();
            viewModel.EnsureSelectedRoom();
            RefreshRoomAndWallPickers();
            LoadCircuitIntoEditor(viewModel.SelectedCircuit);
            SyncView();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Circuiti", $"Errore caricamento circuiti: {ex.Message}", "OK");
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void OnSelectModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.Select;
        UpdateInteractionButtons();
    }

    private void OnRightHandModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.RightHand;
        specialModeHand = HandSide.Right;
        UpdateInteractionButtons();
    }

    private void OnLeftHandModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.LeftHand;
        specialModeHand = HandSide.Left;
        UpdateInteractionButtons();
    }

    private void OnStartModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.Start;
        UpdateInteractionButtons();
    }

    private void OnTopModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.Top;
        UpdateInteractionButtons();
    }

    private void OnRemoveModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.Remove;
        UpdateInteractionButtons();
    }

    private void OnLeftFootModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.LeftFoot;
        UpdateInteractionButtons();
    }

    private void OnRightFootModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.RightFoot;
        UpdateInteractionButtons();
    }

    private async void OnCreateCircuitClicked(object? sender, EventArgs e)
    {
        try
        {
            await viewModel.CreateCircuitAsync(
                CircuitNameEntry.Text,
                GetSelectedDifficulty(),
                CircuitInclinationEntry.Text,
                SuggestNextHoldEnabledSwitch.IsToggled,
                ReadCircuitGlobalsFromEditor(),
                CircuitWallPicker.SelectedItem as WallDefinition);
            LoadCircuitIntoEditor(viewModel.SelectedCircuit);
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            _ = DisplayAlertAsync("Circuiti", ex.Message, "OK");
        }
    }

    private async void OnUpdateCircuitClicked(object? sender, EventArgs e)
    {
        try
        {
            await viewModel.UpdateSelectedCircuitAsync(
                CircuitNameEntry.Text,
                GetSelectedDifficulty(),
                CircuitInclinationEntry.Text,
                SuggestNextHoldEnabledSwitch.IsToggled,
                ReadCircuitGlobalsFromEditor());
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            _ = DisplayAlertAsync("Circuiti", ex.Message, "OK");
        }
    }

    private async void OnDeleteCircuitClicked(object? sender, EventArgs e)
    {
        try
        {
            await viewModel.DeleteSelectedCircuitAsync();
            PrepareNewCircuitEditor();
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            _ = DisplayAlertAsync("Circuiti", ex.Message, "OK");
        }
    }

    private void OnNewCircuitClicked(object? sender, EventArgs e)
    {
        PrepareNewCircuitEditor();
        SyncView();
    }


    private async void OnLaunchCircuitClicked(object? sender, EventArgs e)
    {
        var circuit = viewModel.SelectedCircuit;
        var wall = viewModel.CurrentWall;
        if (circuit is null || wall is null)
        {
            await DisplayAlertAsync("Circuiti", "Seleziona prima un circuito salvato da avviare.", "OK");
            return;
        }

        var roomName = Uri.EscapeDataString(circuit.RoomName ?? wall.RoomName ?? string.Empty);
        var wallId = Uri.EscapeDataString(wall.Id.ToString(CultureInfo.InvariantCulture));
        var circuitId = Uri.EscapeDataString(circuit.Id.ToString(CultureInfo.InvariantCulture));
        await Shell.Current.GoToAsync($"//circuit-runner-page?room={roomName}&wallId={wallId}&circuitId={circuitId}&autoStart=1");
    }

    private void OnCircuitRoomChanged(object? sender, EventArgs e)
    {
        highlightedHole = null;
        viewModel.SetSelectedRoom(CircuitRoomPicker.SelectedItem as string);
        RefreshRoomAndWallPickers();
        LoadCircuitIntoEditor(viewModel.SelectedCircuit);
        SyncView();
    }

    private void OnCircuitDifficultyChanged(object? sender, EventArgs e)
    {
        if (viewModel.SelectedCircuit is not null)
        {
            viewModel.SelectedCircuit.Difficulty = GetSelectedDifficulty();
        }
    }

    private void OnPreviewViewportSizeChanged(object? sender, EventArgs e)
    {
        if (PreviewViewport.Width <= 0 || PreviewViewport.Height <= 0)
        {
            return;
        }

        basePreviewWidth = Math.Max(280d, PreviewViewport.Width - 4d);
        basePreviewHeight = Math.Max(280d, PreviewViewport.Height - 4d);
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
    }

    private void OnPreviewPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                previewZoomStart = previewZoom;
                break;
            case GestureStatus.Running:
                previewZoom = Math.Clamp(previewZoomStart * e.Scale, 1d, 4d);
                UpdatePreviewZoomLayout();
                break;
        }
    }

    private async void OnPreviewSingleTapped(object? sender, TappedEventArgs e)
    {
        switch (interactionMode)
        {
            case CircuitInteractionMode.Select:
                HighlightHoleOnly(e);
                break;
            case CircuitInteractionMode.LeftHand:
                ToggleHoleForHand(e, HandSide.Left, MovementRole.Normal);
                break;
            case CircuitInteractionMode.Start:
                ToggleHoleForHand(e, specialModeHand, MovementRole.Start);
                break;
            case CircuitInteractionMode.Top:
                ToggleHoleForHand(e, specialModeHand, MovementRole.Top);
                break;
            case CircuitInteractionMode.Remove:
                RemoveHole(e);
                break;
            case CircuitInteractionMode.LeftFoot:
                await AssignFootFromTapAsync(e, CurrentStateTarget.LeftFoot);
                break;
            case CircuitInteractionMode.RightFoot:
                await AssignFootFromTapAsync(e, CurrentStateTarget.RightFoot);
                break;
            default:
                ToggleHoleForHand(e, HandSide.Right, MovementRole.Normal);
                break;
        }
    }

    private void OnPreviewDoubleTappedForLeftHand(object? sender, TappedEventArgs e)
    {
        if (interactionMode == CircuitInteractionMode.Select)
        {
            HighlightHoleOnly(e);
            return;
        }

        ToggleHoleForHand(e, HandSide.Left, MovementRole.Normal);
    }

    private void OnPreviewTripleTappedToRemove(object? sender, TappedEventArgs e)
    {
        if (interactionMode == CircuitInteractionMode.Select)
        {
            HighlightHoleOnly(e);
            return;
        }

        RemoveHole(e);
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom + 0.25d, 1d, 4d);
        previewZoomStart = previewZoom;
        UpdatePreviewZoomLayout();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom - 0.25d, 1d, 4d);
        previewZoomStart = previewZoom;
        UpdatePreviewZoomLayout();
    }

    private void OnZoomResetClicked(object? sender, EventArgs e)
    {
        previewZoom = 1d;
        previewZoomStart = 1d;
        UpdatePreviewZoomLayout();
    }

    private async void OnQuickRightClicked(object? sender, EventArgs e)
    {
        await ApplyActionToHighlightedHoleAsync(HandSide.Right, MovementRole.Normal);
    }

    private async void OnQuickLeftClicked(object? sender, EventArgs e)
    {
        await ApplyActionToHighlightedHoleAsync(HandSide.Left, MovementRole.Normal);
    }

    private async void OnQuickStartRightClicked(object? sender, EventArgs e)
    {
        await ApplyActionToHighlightedHoleAsync(HandSide.Right, MovementRole.Start);
    }

    private async void OnQuickStartLeftClicked(object? sender, EventArgs e)
    {
        await ApplyActionToHighlightedHoleAsync(HandSide.Left, MovementRole.Start);
    }

    private async void OnQuickTopRightClicked(object? sender, EventArgs e)
    {
        await ApplyActionToHighlightedHoleAsync(HandSide.Right, MovementRole.Top);
    }

    private async void OnQuickTopLeftClicked(object? sender, EventArgs e)
    {
        await ApplyActionToHighlightedHoleAsync(HandSide.Left, MovementRole.Top);
    }

    private async void OnQuickRemoveClicked(object? sender, EventArgs e)
    {
        await RemoveHighlightedHoleAsync();
    }

    private void SyncView()
    {
        var pageState = pageStateService.Build(viewModel, interactionMode, specialModeHand, CircuitWallPicker.SelectedItem as WallDefinition);
        WorkflowTitleLabel.Text = pageState.WorkflowTitleText;
        WorkflowMessageLabel.Text = pageState.WorkflowMessageText;
        CurrentWallLabel.Text = pageState.CurrentWallLabel;
        EditorModeLabel.Text = pageState.EditorModeText;
        CircuitSummaryLabel.Text = pageState.CircuitSummaryText;
        CreateCircuitButton.IsEnabled = pageState.CanCreateCircuit;
        LaunchCircuitButton.IsEnabled = viewModel.SelectedCircuit is not null;
        UpdateCircuitButton.IsEnabled = pageState.CanUpdateCircuit;
        DeleteCircuitButton.IsEnabled = pageState.CanDeleteCircuit;
        CircuitWallPicker.IsEnabled = pageState.CanPickWall;
        previewDrawable.Wall = viewModel.CurrentWall;
        previewDrawable.Circuit = viewModel.SelectedCircuit;
        previewDrawable.HighlightedHole = highlightedHole;
        var suggestedHole = ResolveSuggestedHole();
        previewDrawable.SelectedHoles = GetCurrentStateHoles()
            .Concat(suggestedHole is null ? Array.Empty<WallHoleDefinition>() : new[] { suggestedHole.Value })
            .GroupBy(hole => hole.Number)
            .Select(group => group.First())
            .ToList();
        previewDrawable.SuggestedHole = suggestedHole;
        SelectedHoleInfoLabel.Text = BuildSelectedHoleInfoText();
        UpdateHighlightedHoleActions();
        UpdateSuggestionUi();
        RefreshRoomAndWallPickers(pageState);

        RebuildCircuitsList();
        RebuildMovementsList();
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
        UpdateInteractionButtons();
    }

    private void RebuildCircuitsList()
    {
        CircuitsHost.Children.Clear();

        foreach (var circuit in viewModel.GetVisibleCircuits())
        {
            var isSelected = ReferenceEquals(circuit, viewModel.SelectedCircuit);
            var border = new Border
            {
                Background = isSelected ? Color.FromArgb("#2A2212") : Color.FromArgb("#191611"),
                Stroke = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#B9922F"),
                StrokeThickness = isSelected ? 3 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = 12
            };

            border.Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = circuit.DisplayLabel,
                        FontSize = 16,
                        TextColor = Color.FromArgb("#F8E7A8")
                    },
                    new Label
                    {
                        Text = $"Parete: {circuit.WallName}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#D8A72D")
                    }
                }
            };

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    viewModel.SelectCircuit(circuit);
                    LoadCircuitIntoEditor(circuit);
                    SyncView();
                })
            });

            CircuitsHost.Children.Add(border);
        }
    }

    private void RebuildMovementsList()
    {
        MovementsHost.Children.Clear();
        var orderedMovements = viewModel.GetOrderedMovements();
        MovementsEmptyLabel.IsVisible = orderedMovements.Count == 0;

        if (orderedMovements.Count == 0)
        {
            return;
        }

        foreach (var movement in orderedMovements)
        {
            MovementsHost.Children.Add(CreateMovementCard(movement));
        }
    }

    private void LoadCircuitIntoEditor(CircuitDefinition? circuit)
    {
        highlightedHole = null;

        if (circuit is null)
        {
            PrepareNewCircuitEditor();
            return;
        }

        CircuitNameEntry.Text = circuit.Name;
        SelectDifficulty(circuit.Difficulty);
        CircuitInclinationEntry.Text = circuit.Inclination;
        SuggestNextHoldEnabledSwitch.IsToggled = circuit.SuggestNextHoldEnabled;
        ApplyCircuitGlobalsToEditor(circuit.Globals);
        ClearSuggestionState();
        viewModel.SetSelectedRoom(viewModel.GetRoomNameForCircuit(circuit));
        RefreshRoomAndWallPickers();
        CircuitWallPicker.SelectedItem = viewModel.AvailableWalls
            .FirstOrDefault(wall =>
                string.Equals(wall.RoomName, circuit.RoomName, StringComparison.Ordinal) &&
                string.Equals(wall.Name, circuit.WallName, StringComparison.Ordinal));
    }

    private void PrepareNewCircuitEditor()
    {
        viewModel.StartNewCircuitDraft();
        highlightedHole = null;
        CircuitNameEntry.Text = viewModel.SuggestedCircuitName;
        CircuitDifficultyPicker.SelectedItem = null;
        CircuitInclinationEntry.Text = string.Empty;
        SuggestNextHoldEnabledSwitch.IsToggled = false;
        ApplyCircuitGlobalsToEditor(((App)Application.Current!).AppSettingsService.Load().CircuitDefaults);
        ClearSuggestionState();
        RefreshRoomAndWallPickers();
        CircuitWallPicker.SelectedItem = viewModel.GetWallsForSelectedRoom().FirstOrDefault();
    }

    private void RefreshRoomAndWallPickers(CircuitPageState? pageState = null)
    {
        viewModel.EnsureSelectedRoom();

        pageState ??= pageStateService.Build(viewModel, interactionMode, specialModeHand, CircuitWallPicker.SelectedItem as WallDefinition);
        CircuitRoomPicker.ItemsSource = pageState.AvailableRooms.ToList();
        var selectedRoom = pageState.SelectedRoomName;
        if (!string.Equals(CircuitRoomPicker.SelectedItem as string, selectedRoom, StringComparison.Ordinal))
        {
            CircuitRoomPicker.SelectedItem = selectedRoom;
        }

        CircuitWallPicker.ItemsSource = pageState.VisibleWalls.ToList();
        CircuitWallPicker.SelectedItem = pageState.SelectedWall;
    }

    private void UpdatePreviewZoomLayout()
    {
        previewDrawable.ZoomFactor = (float)previewZoom;
        var desiredSize = previewDrawable.GetDesiredSize(previewZoom);
        CircuitPreviewCanvas.WidthRequest = Math.Max(basePreviewWidth, desiredSize.Width);
        CircuitPreviewCanvas.HeightRequest = Math.Max(basePreviewHeight, desiredSize.Height);
        CircuitPreviewLayer.WidthRequest = CircuitPreviewCanvas.WidthRequest;
        CircuitPreviewLayer.HeightRequest = CircuitPreviewCanvas.HeightRequest;
        UpdateWallImageOverlay();
        CircuitPreviewCanvas.Invalidate();
    }

    private void OnPickCircuitRightHandColorClicked(object? sender, EventArgs e)
    {
        SetActiveCircuitColorTarget(CircuitColorTarget.RightHand);
    }

    private void OnPickCircuitLeftHandColorClicked(object? sender, EventArgs e)
    {
        SetActiveCircuitColorTarget(CircuitColorTarget.LeftHand);
    }

    private void OnPickCircuitStartColorClicked(object? sender, EventArgs e)
    {
        SetActiveCircuitColorTarget(CircuitColorTarget.Start);
    }

    private void OnPickCircuitTopColorClicked(object? sender, EventArgs e)
    {
        SetActiveCircuitColorTarget(CircuitColorTarget.Top);
    }

    private void OnCircuitColorSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (isUpdatingCircuitColorControls)
        {
            return;
        }

        var color = Color.FromRgb((byte)CircuitRedSlider.Value, (byte)CircuitGreenSlider.Value, (byte)CircuitBlueSlider.Value);
        var hex = ToHexColor(color);
        ApplyCircuitColorToTarget(activeCircuitColorTarget, hex);
        UpdateCircuitColorPickerTexts(color);
    }

    private void OnToggleGlobalsClicked(object? sender, EventArgs e)
    {
        isCircuitGlobalsExpanded = !isCircuitGlobalsExpanded;
        ApplyCircuitGlobalsEditorExpansion();
    }

    private void OnApplyAppDefaultsToCircuitClicked(object? sender, EventArgs e)
    {
        var defaults = ((App)Application.Current!).AppSettingsService.Load().CircuitDefaults;
        ApplyCircuitGlobalsToEditor(defaults);
    }

    private CircuitGlobalsDefinition ReadCircuitGlobalsFromEditor()
    {
        return new CircuitGlobalsDefinition
        {
            PresetName = ReadRequiredText(CircuitPresetNameEntry.Text, "Inserisci un preset name valido."),
            Effect = ReadRequiredText(CircuitEffectEntry.Text, "Inserisci un effect valido."),
            DefaultBrightness = ParseRangeInt(CircuitDefaultBrightnessEntry.Text, 0, 255, "Default brightness deve essere tra 0 e 255."),
            DimmedBrightness = ParseRangeInt(CircuitDimmedBrightnessEntry.Text, 0, 255, "Dimmed brightness deve essere tra 0 e 255."),
            RightHandColor = ParseHexColor(CircuitRightHandColorValueLabel.Text, "Il colore mano destra deve essere in formato #RRGGBB."),
            LeftHandColor = ParseHexColor(CircuitLeftHandColorValueLabel.Text, "Il colore mano sinistra deve essere in formato #RRGGBB."),
            StartColor = ParseHexColor(CircuitStartColorValueLabel.Text, "Il colore start deve essere in formato #RRGGBB."),
            TopColor = ParseHexColor(CircuitTopColorValueLabel.Text, "Il colore top deve essere in formato #RRGGBB."),
            BlinkCount = ParseRangeInt(CircuitBlinkCountEntry.Text, 0, 20, "Blink count deve essere tra 0 e 20."),
            BlinkPeriodMs = ParseRangeInt(CircuitBlinkPeriodMsEntry.Text, 50, 5000, "Blink period deve essere tra 50 e 5000 ms."),
            HoldDurationMs = ParseRangeInt(CircuitHoldDurationMsEntry.Text, 100, 30000, "Hold duration deve essere tra 100 e 30000 ms.")
        };
    }

    private void ApplyCircuitGlobalsToEditor(CircuitGlobalsDefinition globals)
    {
        CircuitPresetNameEntry.Text = globals.PresetName;
        CircuitEffectEntry.Text = globals.Effect;
        CircuitDefaultBrightnessEntry.Text = globals.DefaultBrightness.ToString(CultureInfo.InvariantCulture);
        CircuitDimmedBrightnessEntry.Text = globals.DimmedBrightness.ToString(CultureInfo.InvariantCulture);
        CircuitRightHandColorValueLabel.Text = globals.RightHandColor;
        CircuitLeftHandColorValueLabel.Text = globals.LeftHandColor;
        CircuitStartColorValueLabel.Text = globals.StartColor;
        CircuitTopColorValueLabel.Text = globals.TopColor;
        CircuitBlinkCountEntry.Text = globals.BlinkCount.ToString(CultureInfo.InvariantCulture);
        CircuitBlinkPeriodMsEntry.Text = globals.BlinkPeriodMs.ToString(CultureInfo.InvariantCulture);
        CircuitHoldDurationMsEntry.Text = globals.HoldDurationMs.ToString(CultureInfo.InvariantCulture);
        RefreshCircuitColorPreviews();
        SetActiveCircuitColorTarget(activeCircuitColorTarget);
    }

    private void RefreshCircuitColorPreviews()
    {
        ApplyColorPreview(CircuitRightHandColorPreview, CircuitRightHandColorValueLabel.Text);
        ApplyColorPreview(CircuitLeftHandColorPreview, CircuitLeftHandColorValueLabel.Text);
        ApplyColorPreview(CircuitStartColorPreview, CircuitStartColorValueLabel.Text);
        ApplyColorPreview(CircuitTopColorPreview, CircuitTopColorValueLabel.Text);
    }

    private void ApplyCircuitGlobalsEditorExpansion()
    {
        CircuitGlobalsEditorHost.IsVisible = isCircuitGlobalsExpanded;
        ToggleGlobalsButton.Text = isCircuitGlobalsExpanded ? "Nascondi" : "Mostra";
    }

    private void SetActiveCircuitColorTarget(CircuitColorTarget target)
    {
        activeCircuitColorTarget = target;
        CircuitColorPickerTargetLabel.Text = target switch
        {
            CircuitColorTarget.RightHand => "Stai modificando: mano destra",
            CircuitColorTarget.LeftHand => "Stai modificando: mano sinistra",
            CircuitColorTarget.Start => "Stai modificando: start",
            CircuitColorTarget.Top => "Stai modificando: top",
            _ => "Seleziona un colore da modificare."
        };

        var hex = GetCircuitColorValueForTarget(target);
        if (!TryParseColor(hex, out var color))
        {
            color = Color.FromArgb("#3A3120");
        }

        isUpdatingCircuitColorControls = true;
        CircuitRedSlider.Value = Math.Round(color.Red * 255d);
        CircuitGreenSlider.Value = Math.Round(color.Green * 255d);
        CircuitBlueSlider.Value = Math.Round(color.Blue * 255d);
        isUpdatingCircuitColorControls = false;
        UpdateCircuitColorPickerTexts(color);
    }

    private string GetCircuitColorValueForTarget(CircuitColorTarget target)
    {
        return target switch
        {
            CircuitColorTarget.RightHand => CircuitRightHandColorValueLabel.Text ?? "#C44536",
            CircuitColorTarget.LeftHand => CircuitLeftHandColorValueLabel.Text ?? "#247BA0",
            CircuitColorTarget.Start => CircuitStartColorValueLabel.Text ?? "#FFFF00",
            CircuitColorTarget.Top => CircuitTopColorValueLabel.Text ?? "#FF0000",
            _ => "#3A3120"
        };
    }

    private void ApplyCircuitColorToTarget(CircuitColorTarget target, string hex)
    {
        switch (target)
        {
            case CircuitColorTarget.RightHand:
                CircuitRightHandColorValueLabel.Text = hex;
                break;
            case CircuitColorTarget.LeftHand:
                CircuitLeftHandColorValueLabel.Text = hex;
                break;
            case CircuitColorTarget.Start:
                CircuitStartColorValueLabel.Text = hex;
                break;
            case CircuitColorTarget.Top:
                CircuitTopColorValueLabel.Text = hex;
                break;
        }

        RefreshCircuitColorPreviews();
    }

    private void UpdateCircuitColorPickerTexts(Color color)
    {
        CircuitRedValueLabel.Text = $"Rosso: {(int)Math.Round(color.Red * 255d)}";
        CircuitGreenValueLabel.Text = $"Verde: {(int)Math.Round(color.Green * 255d)}";
        CircuitBlueValueLabel.Text = $"Blu: {(int)Math.Round(color.Blue * 255d)}";
        CircuitColorPickerPreview.Color = color;
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

    private void UpdatePreviewBaseScale()
    {
        var wall = viewModel.CurrentWall;
        if (wall is null || wall.Width <= 0 || wall.Height <= 0)
        {
            previewDrawable.PixelsPerMillimeter = 0.1f;
            return;
        }

        const double padding = 48d;
        var availableWidth = Math.Max(1d, basePreviewWidth - padding);
        var availableHeight = Math.Max(1d, basePreviewHeight - padding);
        var fitScale = Math.Min(availableWidth / wall.Width, availableHeight / wall.Height);
        previewDrawable.PixelsPerMillimeter = (float)Math.Max(0.01d, fitScale);
    }

    private async void RemoveHole(TappedEventArgs e)
    {
        var hole = FindTappedHole(e);
        if (hole is null)
        {
            return;
        }

        highlightedHole = hole;

        await RemoveHighlightedHoleAsync();
    }

    private async void ToggleHoleForHand(TappedEventArgs e, HandSide hand, MovementRole role)
    {
        var hole = FindTappedHole(e);
        if (hole is null)
        {
            return;
        }

        highlightedHole = hole;

        await ApplyActionToHighlightedHoleAsync(hand, role);
    }

    private async Task ApplyActionToHighlightedHoleAsync(HandSide hand, MovementRole role)
    {
        if (highlightedHole is not WallHoleDefinition hole || hole.Number <= 0)
        {
            return;
        }

        var wasAlreadyAssigned = HasMovementForHand(hole, hand);

        try
        {
            await viewModel.ToggleMovementAsync(hole, hand, role);
            AdvanceInteractionModeAfterSuccessfulApply(hand, role, wasAlreadyAssigned);
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Circuiti", ex.Message, "OK");
        }
    }

    private async Task RemoveHighlightedHoleAsync()
    {
        if (highlightedHole is not WallHoleDefinition hole || hole.Number <= 0)
        {
            return;
        }

        try
        {
            await viewModel.RemoveHoleAsync(hole);
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Circuiti", ex.Message, "OK");
        }
    }

    private WallHoleDefinition? FindTappedHole(TappedEventArgs e)
    {
        var position = e.GetPosition(CircuitPreviewCanvas);
        if (position is null)
        {
            return null;
        }

        return previewDrawable.FindNearestHole(position.Value);
    }

    private void HighlightHoleOnly(TappedEventArgs e)
    {
        var hole = FindTappedHole(e);
        if (hole is null)
        {
            return;
        }

        highlightedHole = hole;
        SyncView();
    }

    private void UpdateInteractionButtons()
    {
        SetModeVisual(SelectModeButton, interactionMode == CircuitInteractionMode.Select);
        SetModeVisual(RightHandModeButton, interactionMode == CircuitInteractionMode.RightHand);
        SetModeVisual(LeftHandModeButton, interactionMode == CircuitInteractionMode.LeftHand);
        SetModeVisual(StartModeButton, interactionMode == CircuitInteractionMode.Start);
        SetModeVisual(TopModeButton, interactionMode == CircuitInteractionMode.Top);
        SetModeVisual(LeftFootModeButton, interactionMode == CircuitInteractionMode.LeftFoot);
        SetModeVisual(RightFootModeButton, interactionMode == CircuitInteractionMode.RightFoot);
        SetModeVisual(RemoveModeButton, interactionMode == CircuitInteractionMode.Remove);
        InteractionHintLabel.Text = interactionMode switch
        {
            CircuitInteractionMode.LeftFoot => "Modalita piede SX attiva: tocca un foro sulla parete per salvarlo come appoggio sinistro.",
            CircuitInteractionMode.RightFoot => "Modalita piede DX attiva: tocca un foro sulla parete per salvarlo come appoggio destro.",
            _ => pageStateService.Build(viewModel, interactionMode, specialModeHand, CircuitWallPicker.SelectedItem as WallDefinition).InteractionHintText
        };
    }

    private void OnAssignCurrentLeftFootClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.LeftFoot;
        UpdateInteractionButtons();
    }

    private void OnAssignCurrentRightFootClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.RightFoot;
        UpdateInteractionButtons();
    }

    private void OnClearCurrentStateClicked(object? sender, EventArgs e)
    {
        ClearSuggestionState();
        SyncView();
    }

    private async void OnSuggestNextHoldClicked(object? sender, EventArgs e)
    {
        try
        {
            var wall = viewModel.CurrentWall ?? CircuitWallPicker.SelectedItem as WallDefinition;
            if (wall is null)
            {
                await DisplayAlertAsync("Suggerimento", "Seleziona una parete valida.", "OK");
                return;
            }

            var currentLeftHandStateHole = ResolveCurrentHandStateHole(HandSide.Left);
            var currentRightHandStateHole = ResolveCurrentHandStateHole(HandSide.Right);
            if (currentLeftHandStateHole is null || currentRightHandStateHole is null)
            {
                await DisplayAlertAsync("Suggerimento", "Servono almeno le ultime posizioni di mano SX e mano DX nel circuito.", "OK");
                return;
            }

            var movingHand = DetermineNextMovingHand();
            var circuit = BuildSuggestionCircuitContext(wall);
            var request = new NextHoldSuggestionRequest
            {
                Wall = wall,
                Circuit = circuit,
                MovingHand = movingHand,
                CurrentLeftHandHoleNumber = currentLeftHandStateHole.Value.Number,
                CurrentRightHandHoleNumber = currentRightHandStateHole.Value.Number,
                CurrentLeftFootHoleNumber = currentLeftFootStateHole?.Number,
                CurrentRightFootHoleNumber = currentRightFootStateHole?.Number,
                MaxSuggestions = 3
            };

            lastSuggestionResult = nextHoldSuggestionService.SuggestNextHold(request);
            if (lastSuggestionResult.SuggestedHoleNumber is null)
            {
                await DisplayAlertAsync(
                    "Suggerimento",
                    BuildNoSuggestionMessage(wall, currentLeftHandStateHole.Value.Number, currentRightHandStateHole.Value.Number),
                    "OK");
            }

            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Suggerimento", ex.Message, "OK");
        }
    }

    private async void OnApplySuggestedHoldClicked(object? sender, EventArgs e)
    {
        var suggestedHole = ResolveSuggestedHole();
        if (suggestedHole is null)
        {
            await DisplayAlertAsync("Suggerimento", "Calcola prima una presa suggerita.", "OK");
            return;
        }

        highlightedHole = suggestedHole;
        await ApplyActionToHighlightedHoleAsync(DetermineNextMovingHand(), MovementRole.Normal);
    }

    private static void SetModeVisual(Button button, bool isActive)
    {
        button.BackgroundColor = isActive ? Color.FromArgb("#F2C94C") : Color.FromArgb("#3A3120");
        button.TextColor = isActive ? Color.FromArgb("#14110B") : Color.FromArgb("#F8E7A8");
    }

    private void UpdateWallImageOverlay()
    {
        var wall = viewModel.CurrentWall;
        CircuitPanelImagesHost.Children.Clear();
        if (wall is null)
        {
            return;
        }

        var wallBounds = previewDrawable.GetWallBounds();
        var scale = Math.Max(0.01f, previewDrawable.PixelsPerMillimeter * previewDrawable.ZoomFactor);

        foreach (var panel in wall.Panels.Where(panel => !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)))
        {
            var panelBaseX = wallBounds.X + ((float)panel.X * scale);
            var panelBaseY = wallBounds.Y + ((float)panel.Y * scale);
            var imageWidth = ((float)panel.Width * scale) * (float)Math.Max(0.2d, panel.ImageScale);
            var imageHeight = ((float)panel.Height * scale) * (float)Math.Max(0.2d, panel.ImageScale);
            var cropWidthFactor = panel.EffectiveImageCropWidthFactor;
            var cropHeightFactor = panel.EffectiveImageCropHeightFactor;
            var stretchedWidth = imageWidth / (float)cropWidthFactor;
            var stretchedHeight = imageHeight / (float)cropHeightFactor;
            var imageX = panelBaseX + ((float)panel.ImageOffsetX * scale) - (float)(panel.EffectiveImageCropLeft * stretchedWidth);
            var imageY = panelBaseY + ((float)panel.ImageOffsetY * scale) - (float)(panel.EffectiveImageCropTop * stretchedHeight);

            var image = new Image
            {
                Source = ImageSource.FromFile(panel.ImagePath!),
                Opacity = panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity,
                Aspect = Aspect.Fill,
                InputTransparent = true
            };

            AbsoluteLayout.SetLayoutBounds(image, new Rect(imageX, imageY, stretchedWidth, stretchedHeight));
            AbsoluteLayout.SetLayoutFlags(image, AbsoluteLayoutFlags.None);
            CircuitPanelImagesHost.Children.Add(image);
        }
    }

    private View CreateMovementCard(CircuitMovementDefinition movement)
    {
        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 76 },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };

        var thumbnail = CreateMovementThumbnail(movement);
        if (thumbnail is not null)
        {
            layout.Add(thumbnail);
        }

        layout.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                CreateMovementBadgeRow(movement),
                new Label
                {
                    Text = BuildMovementHeadline(movement),
                    TextColor = Color.FromArgb("#F8E7A8"),
                    FontSize = 14
                },
                new Label
                {
                    Text = $"Foro {movement.HoleNumber} - Parete {movement.WallName}",
                    TextColor = Color.FromArgb("#D8A72D"),
                    FontSize = 12
                },
                BuildMovementMetadataLabel(movement),
                new Label
                {
                    Text = $"{GetMovementRoleText(movement.Role)} {GetHandShortLabel(movement.Hand)} - Sequenza {movement.Sequence:00}",
                    TextColor = Color.FromArgb("#B9AA79"),
                    FontSize = 11
                }
            }
        }, 1);

        return new Border
        {
            Background = Color.FromArgb("#191611"),
            Stroke = GetMovementRoleColor(movement),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 10,
            Content = layout
        };
    }

    private static View CreateMovementBadgeRow(CircuitMovementDefinition movement)
    {
        var handColor = GetHandColor(movement.Hand);
        var roleColor = GetRoleColor(movement.Role);
        var roleText = GetMovementRoleBadgeText(movement.Role);

        return new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Border
                {
                    Background = handColor,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(8, 2),
                    Content = new Label
                    {
                        Text = GetHandShortLabel(movement.Hand),
                        FontSize = 11,
                        TextColor = Color.FromArgb("#14110B"),
                        FontFamily = "OpenSansSemibold"
                    }
                },
                new Border
                {
                    Background = Color.FromArgb("#2B2418"),
                    Stroke = Color.FromArgb("#8E7531"),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(8, 2),
                    Content = new Label
                    {
                        Text = $"SEQ {movement.Sequence:00}",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#F8E7A8"),
                        FontFamily = "OpenSansSemibold"
                    }
                },
                new Border
                {
                    Background = roleColor,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(8, 2),
                    Content = new Label
                    {
                        Text = roleText,
                        FontSize = 11,
                        TextColor = movement.Role == MovementRole.Top ? Color.FromArgb("#14110B") : Color.FromArgb("#F8E7A8"),
                        FontFamily = "OpenSansSemibold"
                    }
                }
            }
        };
    }

    private static Color GetMovementRoleColor(CircuitMovementDefinition movement)
    {
        return movement.Role switch
        {
            MovementRole.Start => Color.FromArgb("#2E8B57"),
            MovementRole.Top => Color.FromArgb("#F2C94C"),
            _ => GetHandColor(movement.Hand)
        };
    }

    private void UpdateHighlightedHoleActions()
    {
        var canEditHighlightedHole =
            highlightedHole is WallHoleDefinition hole &&
            hole.Number > 0 &&
            viewModel.SelectedCircuit is not null;

        SelectedHoleActionsHost.IsVisible = canEditHighlightedHole;
        QuickRightButton.IsEnabled = canEditHighlightedHole;
        QuickLeftButton.IsEnabled = canEditHighlightedHole;
        QuickRemoveButton.IsEnabled = canEditHighlightedHole;
        QuickStartRightButton.IsEnabled = canEditHighlightedHole;
        QuickStartLeftButton.IsEnabled = canEditHighlightedHole;
        QuickTopRightButton.IsEnabled = canEditHighlightedHole;
        QuickTopLeftButton.IsEnabled = canEditHighlightedHole;
    }

    private string BuildSelectedHoleInfoText()
    {
        if (highlightedHole is not WallHoleDefinition hole || hole.Number <= 0)
        {
            return "Nessun foro selezionato.";
        }

        var movements = viewModel.SelectedCircuit?.Movements
            .Where(item => string.Equals(item.WallName, viewModel.CurrentWall?.Name, StringComparison.Ordinal))
            .Where(item => item.HoleNumber == hole.Number)
            .OrderBy(item => item.Hand)
            .ThenBy(item => item.Sequence)
            .ToList();

        if (movements is null || movements.Count == 0)
        {
            return $"Foro {hole.Number} - {hole.HoldSummary} - pannello {hole.PanelName} - X {hole.AbsoluteX:0.#} mm - Y {hole.AbsoluteY:0.#} mm";
        }

        var states = string.Join(" | ", movements.Select(movement =>
        {
            var roleLabel = movement.Role switch
            {
                MovementRole.Start => "Start",
                MovementRole.Top => "Top",
                _ => "Mov"
            };

            var handLabel = movement.Hand == HandSide.Left ? "SX" : "DX";
            return $"{roleLabel} {handLabel} seq {movement.Sequence:00}";
        }));

        return $"Foro {hole.Number} - {hole.HoldSummary} - {states} - pannello {hole.PanelName}";
    }

    private bool HasMovementForHand(WallHoleDefinition hole, HandSide hand)
    {
        return viewModel.SelectedCircuit?.Movements.Any(movement =>
            string.Equals(movement.WallName, viewModel.CurrentWall?.Name, StringComparison.Ordinal) &&
            movement.HoleNumber == hole.Number &&
            movement.Hand == hand) == true;
    }

    private void AdvanceInteractionModeAfterSuccessfulApply(HandSide hand, MovementRole role, bool wasAlreadyAssigned)
    {
        if (wasAlreadyAssigned)
        {
            return;
        }

        if (role == MovementRole.Start)
        {
            var hasRightStart = HasRoleForHand(MovementRole.Start, HandSide.Right);
            var hasLeftStart = HasRoleForHand(MovementRole.Start, HandSide.Left);

            if (!hasRightStart)
            {
                interactionMode = CircuitInteractionMode.Start;
                specialModeHand = HandSide.Right;
                return;
            }

            if (!hasLeftStart)
            {
                interactionMode = CircuitInteractionMode.Start;
                specialModeHand = HandSide.Left;
                return;
            }

            interactionMode = hand == HandSide.Right
                ? CircuitInteractionMode.LeftHand
                : CircuitInteractionMode.RightHand;
            specialModeHand = hand == HandSide.Right ? HandSide.Left : HandSide.Right;
            return;
        }

        if (role != MovementRole.Normal)
        {
            return;
        }

        if (interactionMode == CircuitInteractionMode.RightHand || interactionMode == CircuitInteractionMode.LeftHand)
        {
            var nextHand = hand == HandSide.Right ? HandSide.Left : HandSide.Right;
            interactionMode = nextHand == HandSide.Right
                ? CircuitInteractionMode.RightHand
                : CircuitInteractionMode.LeftHand;
            specialModeHand = nextHand;
        }
    }

    private bool HasRoleForHand(MovementRole role, HandSide hand)
    {
        return viewModel.SelectedCircuit?.Movements.Any(movement =>
            movement.Role == role &&
            movement.Hand == hand) == true;
    }

    private View? CreateMovementThumbnail(CircuitMovementDefinition movement)
    {
        var wall = viewModel.CurrentWall;
        if (wall is null || wall.Name != movement.WallName)
        {
            return null;
        }

        var hole = wall.GetOrderedHoles().FirstOrDefault(item => item.Number == movement.HoleNumber);
        if (hole.Number == 0)
        {
            return null;
        }

        var panel = wall.FindPanel(hole);
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
        {
            return null;
        }

        const double thumbnailSize = 72d;
        var thumbnailSource = TryCreateMovementThumbnailSource(wall, hole, thumbnailSize);
        if (thumbnailSource is null)
        {
            return null;
        }

        var image = new Image
        {
            Source = thumbnailSource,
            Aspect = Aspect.AspectFill,
            WidthRequest = thumbnailSize,
            HeightRequest = thumbnailSize
        };

        var canvas = new Grid
        {
            WidthRequest = thumbnailSize,
            HeightRequest = thumbnailSize,
            Clip = new RectangleGeometry(new Rect(0, 0, thumbnailSize, thumbnailSize)),
            Children =
            {
                image,
                new BoxView
                {
                    WidthRequest = 8,
                    HeightRequest = 8,
                    CornerRadius = 4,
                    Color = movement.Role switch
                    {
                        MovementRole.Start => Color.FromArgb("#2E8B57"),
                        MovementRole.Top => Color.FromArgb("#D4A017"),
                        _ => GetHandColor(movement.Hand)
                    },
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };

        return new Border
        {
            WidthRequest = thumbnailSize,
            HeightRequest = thumbnailSize,
            Stroke = Color.FromArgb("#B9922F"),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 0,
            Content = canvas
        };
    }

    private ImageSource? TryCreateMovementThumbnailSource(WallDefinition wall, WallHoleDefinition hole, double thumbnailSize)
    {
        var panel = wall.FindPanel(hole);
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath))
        {
            return null;
        }

        var pixelSize = TryGetImagePixelSize(panel.ImagePath);
        if (pixelSize is null)
        {
            return ImageSource.FromFile(panel.ImagePath);
        }

        var sourceWidth = Math.Max(1d, pixelSize.Value.Width);
        var sourceHeight = Math.Max(1d, pixelSize.Value.Height);
        var imageScale = Math.Max(0.2d, panel.ImageScale);
        var overlayWidth = Math.Max(1d, panel.Width * imageScale);
        var overlayHeight = Math.Max(1d, panel.Height * imageScale);
        var cropWidthPx = sourceWidth * panel.EffectiveImageCropWidthFactor;
        var cropHeightPx = sourceHeight * panel.EffectiveImageCropHeightFactor;
        var holeOverlayX = hole.RelativeX - panel.ImageOffsetX;
        var holeOverlayY = hole.RelativeY - panel.ImageOffsetY;
        var sourcePoint = panel.MapPanelPointToImageSource(holeOverlayX / overlayWidth, holeOverlayY / overlayHeight, sourceWidth, sourceHeight);
        var sourceHoleX = sourcePoint.X;
        var sourceHoleY = sourcePoint.Y;

#if ANDROID
        try
        {
            using var bitmap = Android.Graphics.BitmapFactory.DecodeFile(panel.ImagePath);
            if (bitmap is null)
            {
                return ImageSource.FromFile(panel.ImagePath);
            }

            const double cropWindowMillimeters = 240d;
            var cropScaleX = cropWidthPx / overlayWidth;
            var cropScaleY = cropHeightPx / overlayHeight;
            var cropSizePx = (int)Math.Round(cropWindowMillimeters * ((cropScaleX + cropScaleY) / 2d));
            cropSizePx = Math.Max(96, cropSizePx);
            cropSizePx = Math.Min(cropSizePx, Math.Min(bitmap.Width, bitmap.Height));

            var cropLeft = (int)Math.Round(sourceHoleX - (cropSizePx / 2d));
            var cropTop = (int)Math.Round(sourceHoleY - (cropSizePx / 2d));
            cropLeft = Math.Clamp(cropLeft, 0, Math.Max(0, bitmap.Width - cropSizePx));
            cropTop = Math.Clamp(cropTop, 0, Math.Max(0, bitmap.Height - cropSizePx));

            using var croppedBitmap = Android.Graphics.Bitmap.CreateBitmap(bitmap, cropLeft, cropTop, cropSizePx, cropSizePx);
            using var scaledBitmap = Android.Graphics.Bitmap.CreateScaledBitmap(croppedBitmap, (int)thumbnailSize * 2, (int)thumbnailSize * 2, true);
            using var stream = new MemoryStream();
            scaledBitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream);
            var imageBytes = stream.ToArray();
            return ImageSource.FromStream(() => new MemoryStream(imageBytes));
        }
        catch
        {
            return ImageSource.FromFile(panel.ImagePath);
        }
#else
        return ImageSource.FromFile(panel.ImagePath);
#endif
    }

    private static Size? TryGetImagePixelSize(string imagePath)
    {
#if ANDROID
        try
        {
            var options = new Android.Graphics.BitmapFactory.Options
            {
                InJustDecodeBounds = true
            };

            Android.Graphics.BitmapFactory.DecodeFile(imagePath, options);
            if (options.OutWidth > 0 && options.OutHeight > 0)
            {
                return new Size(options.OutWidth, options.OutHeight);
            }
        }
        catch
        {
        }
#endif

        return null;
    }

    private static View BuildErrorView(string title, Exception ex)
    {
        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 22
                    },
                    new Label
                    {
                        Text = ex.ToString()
                    }
                }
            }
        };
    }

    private static string BuildMovementHeadline(CircuitMovementDefinition movement)
    {
        return $"{GetMovementRoleText(movement.Role)} {GetHandShortLabel(movement.Hand)} - Foro {movement.HoleNumber}";
    }

    private View BuildMovementMetadataLabel(CircuitMovementDefinition movement)
    {
        var wall = viewModel.CurrentWall;
        if (wall is null || !string.Equals(wall.Name, movement.WallName, StringComparison.Ordinal))
        {
            return new Label
            {
                Text = "Metadata presa non disponibili",
                TextColor = Color.FromArgb("#B9AA79"),
                FontSize = 11
            };
        }

        var hole = wall.GetOrderedHoles().FirstOrDefault(item => item.Number == movement.HoleNumber);
        if (hole.Number == 0)
        {
            return new Label
            {
                Text = "Presa non trovata sulla parete",
                TextColor = Color.FromArgb("#B9AA79"),
                FontSize = 11
            };
        }

        return new Label
        {
            Text = hole.HasEstimatedHoldMetadata
                ? $"Presa: {hole.HoldSummary}"
                : $"Presa: {hole.HoldSummary}",
            TextColor = hole.HasEstimatedHoldMetadata ? Color.FromArgb("#F2C94C") : Color.FromArgb("#7ED6A1"),
            FontSize = 11
        };
    }

    private async Task AssignHighlightedHoleToCurrentStateAsync(CurrentStateTarget target)
    {
        if (highlightedHole is not WallHoleDefinition hole || hole.Number <= 0)
        {
            return;
        }

        switch (target)
        {
            case CurrentStateTarget.LeftFoot:
                currentLeftFootStateHole = hole;
                break;
            case CurrentStateTarget.RightFoot:
                currentRightFootStateHole = hole;
                break;
        }

        lastSuggestionResult = null;
        SyncView();
    }

    private void UpdateSuggestionUi()
    {
        var currentLeftHandStateHole = ResolveCurrentHandStateHole(HandSide.Left);
        var currentRightHandStateHole = ResolveCurrentHandStateHole(HandSide.Right);
        CurrentClimberStateLabel.Text =
            $"Mani: SX {FormatHoleLabel(currentLeftHandStateHole)}, DX {FormatHoleLabel(currentRightHandStateHole)} | " +
            $"Piedi: SX {FormatHoleLabel(currentLeftFootStateHole)}, DX {FormatHoleLabel(currentRightFootStateHole)}";

        SuggestionModeInfoLabel.Text = interactionMode switch
        {
            CircuitInteractionMode.LeftFoot => "Tocca ora un foro sulla parete: verra' salvato come piede SX senza modificare la prossima mano.",
            CircuitInteractionMode.RightFoot => "Tocca ora un foro sulla parete: verra' salvato come piede DX senza modificare la prossima mano.",
            _ when viewModel.SelectedCircuit?.SuggestNextHoldEnabled == true || SuggestNextHoldEnabledSwitch.IsToggled
                => $"Circuito pronto al suggerimento. Mano prevista: {(DetermineNextMovingHand() == HandSide.Right ? "DX" : "SX")}.",
            _ => $"Suggerimento manuale disponibile. Mano prevista: {(DetermineNextMovingHand() == HandSide.Right ? "DX" : "SX")}."
        };

        SuggestNextHoldButton.IsEnabled =
            (viewModel.SelectedCircuit is not null || CircuitWallPicker.SelectedItem is WallDefinition) &&
            currentLeftHandStateHole is not null &&
            currentRightHandStateHole is not null;
        ApplySuggestedHoldButton.IsEnabled = lastSuggestionResult?.SuggestedHoleNumber is not null && viewModel.SelectedCircuit is not null;

        var suggestedHole = ResolveSuggestedHole();
        SuggestionResultLabel.Text = lastSuggestionResult is null || suggestedHole is null
            ? "Nessun suggerimento calcolato."
            : $"Suggerita presa foro {suggestedHole.Value.Number} con mano {(DetermineNextMovingHand() == HandSide.Right ? "DX" : "SX")} | {lastSuggestionResult.PrimaryReason}. {lastSuggestionResult.SecondaryReason}. Baricentro: affidabilita {lastSuggestionResult.CenterConfidenceLabel}.";
    }

    private async Task AssignFootFromTapAsync(TappedEventArgs e, CurrentStateTarget target)
    {
        var hole = FindTappedHole(e);
        if (hole is null)
        {
            return;
        }

        highlightedHole = hole;
        await AssignHighlightedHoleToCurrentStateAsync(target);
        interactionMode = CircuitInteractionMode.Select;
        UpdateInteractionButtons();
    }

    private IReadOnlyList<WallHoleDefinition> GetCurrentStateHoles()
    {
        return new[]
        {
            ResolveCurrentHandStateHole(HandSide.Left),
            ResolveCurrentHandStateHole(HandSide.Right),
            currentLeftFootStateHole,
            currentRightFootStateHole
        }
        .Where(hole => hole.HasValue && hole.Value.Number > 0)
        .Select(hole => hole!.Value)
        .ToList();
    }

    private WallHoleDefinition? ResolveSuggestedHole()
    {
        if (lastSuggestionResult?.SuggestedHoleNumber is null || viewModel.CurrentWall is null)
        {
            return null;
        }

        var suggested = viewModel.CurrentWall.GetOrderedHoles()
            .FirstOrDefault(hole => hole.Number == lastSuggestionResult.SuggestedHoleNumber.Value);
        return suggested.Number == 0 ? null : suggested;
    }

    private void ClearSuggestionState()
    {
        currentLeftFootStateHole = null;
        currentRightFootStateHole = null;
        lastSuggestionResult = null;
    }

    private CircuitDefinition BuildSuggestionCircuitContext(WallDefinition wall)
    {
        if (viewModel.SelectedCircuit is not null)
        {
            viewModel.SelectedCircuit.Difficulty = GetSelectedDifficulty();
            viewModel.SelectedCircuit.Inclination = CircuitInclinationEntry.Text?.Trim() ?? string.Empty;
            viewModel.SelectedCircuit.SuggestNextHoldEnabled = SuggestNextHoldEnabledSwitch.IsToggled;
            return viewModel.SelectedCircuit;
        }

        return new CircuitDefinition
        {
            Name = CircuitNameEntry.Text?.Trim() ?? "Circuito bozza",
            Difficulty = GetSelectedDifficulty(),
            Inclination = CircuitInclinationEntry.Text?.Trim() ?? string.Empty,
            SuggestNextHoldEnabled = SuggestNextHoldEnabledSwitch.IsToggled,
            RoomName = wall.RoomName,
            WallName = wall.Name
        };
    }

    private static string BuildNoSuggestionMessage(WallDefinition wall, int leftHandHoleNumber, int rightHandHoleNumber)
    {
        var enabledHoles = wall.GetOrderedHoles()
            .Where(hole => hole.IsEnabled)
            .ToList();
        var holdHoles = enabledHoles
            .Where(hole => hole.HasHold)
            .ToList();
        var handCandidateHoles = holdHoles
            .Where(hole => hole.HoldType != HoldType.Foothold)
            .ToList();
        var freeHandCandidateCount = handCandidateHoles
            .Count(hole => hole.Number != leftHandHoleNumber && hole.Number != rightHandHoleNumber);

        if (enabledHoles.Count == 0)
        {
            return "La parete non ha fori attivi.";
        }

        if (holdHoles.Count == 0)
        {
            return "Su questa parete nessun foro ha una presa assegnata. Il suggerimento usa solo fori con presa presente.";
        }

        if (handCandidateHoles.Count == 0)
        {
            return "Le prese presenti sono tutte marcate come piedi. Per suggerire una mano servono prese non 'Piedi'.";
        }

        if (freeHandCandidateCount == 0)
        {
            return "Non ci sono altre prese candidate oltre a quelle gia' usate da mano SX e mano DX.";
        }

        return $"Nessuna presa candidata trovata con lo stato attuale. Fori attivi: {enabledHoles.Count}, prese presenti: {holdHoles.Count}, prese valide per le mani: {handCandidateHoles.Count}, alternative libere: {freeHandCandidateCount}.";
    }

    private string GetSelectedDifficulty()
    {
        return ClimbingGradeScale.NormalizeOrEmpty(CircuitDifficultyPicker.SelectedItem as string);
    }

    private void SelectDifficulty(string? difficulty)
    {
        var normalized = ClimbingGradeScale.NormalizeOrEmpty(difficulty);
        CircuitDifficultyPicker.SelectedItem = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private HandSide DetermineNextMovingHand()
    {
        var circuit = viewModel.SelectedCircuit;
        if (circuit is null)
        {
            return HandSide.Right;
        }

        var lastNormalMovement = circuit.Movements
            .Where(movement => movement.Role == MovementRole.Normal)
            .OrderBy(movement => movement.Sequence)
            .LastOrDefault();

        return lastNormalMovement is null || lastNormalMovement.Hand == HandSide.Left
            ? HandSide.Right
            : HandSide.Left;
    }

    private static string FormatHoleLabel(WallHoleDefinition? hole)
    {
        return hole is null ? "-" : hole.Value.Number.ToString(CultureInfo.InvariantCulture);
    }

    private static WallHoleDefinition? ResolveHoleFromCircuit(CircuitDefinition circuit, IReadOnlyList<WallHoleDefinition> holes, HandSide hand, MovementRole role)
    {
        var movement = circuit.Movements
            .Where(item => item.Hand == hand && item.Role == role)
            .OrderBy(item => item.Sequence)
            .FirstOrDefault();
        if (movement is null)
        {
            return null;
        }

        var hole = holes.FirstOrDefault(item => item.Number == movement.HoleNumber);
        return hole.Number == 0 ? null : hole;
    }

    private WallHoleDefinition? ResolveCurrentHandStateHole(HandSide hand)
    {
        var circuit = viewModel.SelectedCircuit;
        var wall = viewModel.CurrentWall;
        if (circuit is null || wall is null)
        {
            return null;
        }

        var holes = wall.GetOrderedHoles();
        var lastHandMovement = circuit.Movements
            .Where(item => item.Hand == hand && item.Role != MovementRole.Top)
            .OrderBy(item => item.Sequence)
            .LastOrDefault();

        if (lastHandMovement is not null)
        {
            var lastHole = holes.FirstOrDefault(item => item.Number == lastHandMovement.HoleNumber);
            if (lastHole.Number > 0)
            {
                return lastHole;
            }
        }

        return ResolveHoleFromCircuit(circuit, holes, hand, MovementRole.Start);
    }

    private enum CurrentStateTarget
    {
        LeftFoot,
        RightFoot
    }

    private static string GetMovementRoleText(MovementRole role)
    {
        return role switch
        {
            MovementRole.Start => "Start",
            MovementRole.Top => "Top",
            _ => "Movimento"
        };
    }

    private static string GetMovementRoleBadgeText(MovementRole role)
    {
        return role switch
        {
            MovementRole.Start => "START",
            MovementRole.Top => "TOP",
            _ => "MOVE"
        };
    }

    private static string GetHandShortLabel(HandSide hand)
    {
        return hand == HandSide.Right ? "DX" : "SX";
    }

    private static Color GetHandColor(HandSide hand)
    {
        return hand == HandSide.Left ? Color.FromArgb("#247BA0") : Color.FromArgb("#C44536");
    }

    private static Color GetRoleColor(MovementRole role)
    {
        return role switch
        {
            MovementRole.Start => Color.FromArgb("#2E8B57"),
            MovementRole.Top => Color.FromArgb("#F2C94C"),
            _ => Color.FromArgb("#3A3120")
        };
    }
}
