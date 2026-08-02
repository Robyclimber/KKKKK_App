using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using System.Globalization;
using RouteLab.Drawing;
using RouteLab.Models;
using RouteLab.Services;
using RouteLab.ViewModels;

namespace RouteLab;

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
    private readonly CircuitEditorViewModel viewModel;
    private readonly CircuitEditorDrawable previewDrawable = new();
    private double previewZoom = 1d;
    private double previewZoomStart = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;
    private CircuitInteractionMode interactionMode = CircuitInteractionMode.Select;
    private HandSide specialModeHand = HandSide.Right;
    private WallHoleDefinition? highlightedHole;
    private bool isRefreshing;
    private bool isCircuitGlobalsExpanded = false;
    private CircuitColorTarget activeCircuitColorTarget = CircuitColorTarget.RightHand;
    private bool isUpdatingCircuitColorControls;
    private readonly List<string> selectedCircuitWallNames = new();
    private bool isUpdatingWallControls;
    private readonly SemaphoreSlim movementThumbnailSemaphore = new(1, 1);
    private readonly Dictionary<string, Task<ImageSource?>> movementThumbnailTasks = new(StringComparer.Ordinal);
    private CancellationTokenSource movementThumbnailCancellation = new();
    private readonly Dictionary<string, Image> panelPreviewImages = new(StringComparer.Ordinal);
    private string? panelPreviewWallKey;
    private bool isEditorOpen;

    public CircuitPage()
    {
        try
        {
            InitializeComponent();

            var app = (App)Application.Current!;
            pageStateService = app.CircuitPageStateService;
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
            var busyIndicatorService = ((App)Application.Current!).BusyIndicatorService;
            var wallRepository = new Services.SqliteWallRepository(databaseFactory, busyIndicatorService);
            var roomRepository = new Services.SqliteRoomRepository(databaseFactory, busyIndicatorService);
            pageStateService = new Services.CircuitPageStateService();
            viewModel = new ViewModels.CircuitEditorViewModel(
                new Services.CircuitEditingService(),
                new ViewModels.GymSetupViewModel(
                    new Services.GymSetupService(),
                    new Services.WallConfigurationStorageService(wallRepository),
                    wallRepository,
                    roomRepository),
                new Services.SqliteCircuitRepository(databaseFactory, busyIndicatorService));
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

        using var busy = AppBusy.Show("Caricamento circuiti...");
        try
        {
            isRefreshing = true;
            isEditorOpen = false;
            ResetMovementThumbnailQueue();
            ClearPanelImageOverlay();
            await viewModel.LoadCircuitsAsync();
            viewModel.EnsureSelectedRoom();
            selectedCircuitWallNames.Clear();
            highlightedHole = null;
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

    private void OnFeetModeClicked(object? sender, EventArgs e)
    {
        interactionMode = CircuitInteractionMode.Feet;
        UpdateInteractionButtons();
    }

    private async void OnCreateCircuitClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Creazione circuito...");
        try
        {
            await viewModel.CreateCircuitAsync(
                CircuitNameEntry.Text,
                GetSelectedDifficulty(),
                CircuitInclinationEntry.Text,
                ClimberProfileDefinition.DefaultProfileId,
                false,
                ReadCircuitGlobalsFromEditor(),
                GetSelectedCircuitWalls());
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
        using var busy = AppBusy.Show("Salvataggio circuito...");
        try
        {
            await viewModel.UpdateSelectedCircuitAsync(
                CircuitNameEntry.Text,
                GetSelectedDifficulty(),
                CircuitInclinationEntry.Text,
                viewModel.SelectedCircuit?.ClimberProfileId,
                viewModel.SelectedCircuit?.SuggestNextHoldEnabled ?? false,
                ReadCircuitGlobalsFromEditor(),
                GetSelectedCircuitWalls());
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            _ = DisplayAlertAsync("Circuiti", ex.Message, "OK");
        }
    }

    protected override void OnDisappearing()
    {
        movementThumbnailCancellation.Cancel();
        ClearPanelImageOverlay();
        base.OnDisappearing();
    }

    private void OnSaveCircuitClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedCircuit is null)
        {
            OnCreateCircuitClicked(sender, e);
            return;
        }

        OnUpdateCircuitClicked(sender, e);
    }

    private async void OnDeleteCircuitClicked(object? sender, EventArgs e)
    {
        var circuit = viewModel.SelectedCircuit;
        if (circuit is null)
        {
            await DisplayAlertAsync("Elimina circuito", "Apri prima il circuito che vuoi eliminare.", "OK");
            return;
        }

        var confirmed = await DisplayAlertAsync(
            "Elimina circuito",
            $"Vuoi eliminare il circuito {circuit.Name}?",
            "Elimina",
            "Annulla");
        if (!confirmed)
        {
            return;
        }

        using var busy = AppBusy.Show("Eliminazione circuito...");
        try
        {
            await viewModel.DeleteSelectedCircuitAsync();
            isEditorOpen = false;
            viewModel.StartNewCircuitDraft();
            selectedCircuitWallNames.Clear();
            highlightedHole = null;
            ResetMovementThumbnailQueue();
            ClearPanelImageOverlay();
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            _ = DisplayAlertAsync("Circuiti", ex.Message, "OK");
        }
    }

    private async void OnNewCircuitClicked(object? sender, EventArgs e)
    {
        if (viewModel.GetWallsForSelectedRoom().Count == 0)
        {
            await DisplayAlertAsync("Nuovo circuito", "Configura prima almeno una parete nella sala selezionata.", "OK");
            return;
        }

        PrepareNewCircuitEditor();
        isEditorOpen = true;
        SyncView();
        await CircuitPageScrollView.ScrollToAsync(CircuitEditorSection, ScrollToPosition.Start, true);
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

    private async void OnOpenNextHoldSuggestionClicked(object? sender, EventArgs e)
    {
        var circuit = viewModel.SelectedCircuit;
        if (circuit is null || circuit.Id <= 0)
        {
            await DisplayAlertAsync("Presa successiva", "Salva prima il circuito.", "OK");
            return;
        }

        var circuitId = Uri.EscapeDataString(circuit.Id.ToString(CultureInfo.InvariantCulture));
        var wallName = Uri.EscapeDataString(viewModel.CurrentWall?.Name ?? circuit.WallName);
        await Shell.Current.GoToAsync($"next-hold-suggestion-page?circuitId={circuitId}&wallName={wallName}");
    }


    private void OnCircuitRoomChanged(object? sender, EventArgs e)
    {
        if (isUpdatingWallControls)
        {
            return;
        }

        highlightedHole = null;
        isEditorOpen = false;
        ResetMovementThumbnailQueue();
        ClearPanelImageOverlay();
        viewModel.SetSelectedRoom(CircuitRoomPicker.SelectedItem as string);
        viewModel.StartNewCircuitDraft();
        selectedCircuitWallNames.Clear();
        SyncView();
    }

    private void OnCircuitWallChanged(object? sender, EventArgs e)
    {
        if (isUpdatingWallControls || CircuitWallPicker.SelectedItem is not WallDefinition wall)
        {
            return;
        }

        try
        {
            highlightedHole = null;
            ResetMovementThumbnailQueue();
            viewModel.SetActiveWall(wall);
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            _ = DisplayAlertAsync("Parete attiva", ex.Message, "OK");
        }
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
            case CircuitInteractionMode.Feet:
                await ToggleFootHoldFromTapAsync(e);
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

    private async void OnQuickFeetClicked(object? sender, EventArgs e)
    {
        await ToggleHighlightedFootHoldAsync();
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
        RefreshRoomAndWallPickers();
        var pageState = pageStateService.Build(viewModel, interactionMode, specialModeHand, CircuitWallPicker.SelectedItem as WallDefinition);
        WorkflowTitleLabel.Text = pageState.WorkflowTitleText;
        WorkflowMessageLabel.Text = pageState.WorkflowMessageText;
        CurrentWallLabel.Text = pageState.CurrentWallLabel;
        EditorModeLabel.Text = pageState.EditorModeText;
        CircuitSummaryLabel.Text = pageState.CircuitSummaryText;
        CircuitActions.CanSave = viewModel.SelectedCircuit is null
            ? isEditorOpen && pageState.CanCreateCircuit && selectedCircuitWallNames.Count > 0
            : isEditorOpen && pageState.CanUpdateCircuit;
        CircuitActions.CanDelete = isEditorOpen && pageState.CanDeleteCircuit;
        LaunchCircuitButton.IsEnabled = viewModel.SelectedCircuit is not null;
        OpenNextHoldSuggestionButton.IsEnabled = viewModel.SelectedCircuit is not null;
        var circuitCount = pageState.VisibleCircuits.Count;
        CircuitsCountLabel.Text = circuitCount == 1 ? "1 circuito" : $"{circuitCount} circuiti";
        CircuitsEmptyLabel.IsVisible = circuitCount == 0;
        CircuitWallPicker.IsEnabled = pageState.CanPickWall;
        SetEditorSectionsVisibility();
        RebuildCircuitsList();
        if (!isEditorOpen)
        {
            MovementsHost.Children.Clear();
            MovementsEmptyLabel.IsVisible = true;
            ClearPanelImageOverlay();
            return;
        }

        previewDrawable.Wall = CircuitWallPicker.SelectedItem as WallDefinition ?? viewModel.CurrentWall;
        previewDrawable.Circuit = viewModel.SelectedCircuit;
        previewDrawable.HighlightedHole = highlightedHole;
        previewDrawable.SelectedHoles = GetCurrentStateHoles();
        previewDrawable.SuggestedHole = null;
        SelectedHoleInfoLabel.Text = BuildSelectedHoleInfoText();
        UpdateHighlightedHoleActions();
        RebuildMovementsList();
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
        UpdateInteractionButtons();
    }

    private void SetEditorSectionsVisibility()
    {
        CircuitEditorSection.IsVisible = isEditorOpen;
        CircuitInteractionSection.IsVisible = isEditorOpen;
        CircuitPreviewSection.IsVisible = isEditorOpen;
        CircuitMovementsSection.IsVisible = isEditorOpen;
    }

    private void RebuildCircuitsList()
    {
        CircuitsHost.Children.Clear();
        var settings = ((App)Application.Current!).AppSettingsService.Load();

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

            var openButton = new Button
            {
                Text = "Apri circuito",
                Style = (Style)Application.Current!.Resources["PrimaryActionButtonStyle"]
            };
            openButton.Clicked += async (_, _) =>
            {
                using var busy = AppBusy.Show("Apertura circuito...");
                await Task.Yield();
                isEditorOpen = true;
                viewModel.SelectCircuit(circuit);
                LoadCircuitIntoEditor(circuit);
                SyncView();
                await CircuitPageScrollView.ScrollToAsync(CircuitEditorSection, ScrollToPosition.Start, true);
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
                        Text = $"Pareti: {circuit.WallSummary} | Profilo: {settings.ResolveClimberProfile(circuit.ClimberProfileId).Name}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#D8A72D")
                    },
                    openButton
                }
            };

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
        ResetMovementThumbnailQueue();
        highlightedHole = null;

        if (circuit is null)
        {
            PrepareNewCircuitEditor();
            return;
        }

        CircuitNameEntry.Text = circuit.Name;
        SelectDifficulty(circuit.Difficulty);
        CircuitInclinationEntry.Text = circuit.Inclination;
        ApplyCircuitGlobalsToEditor(circuit.Globals);
        viewModel.SetSelectedRoom(viewModel.GetRoomNameForCircuit(circuit));
        SetSelectedCircuitWallNames(circuit.GetWallNames());
    }

    private void PrepareNewCircuitEditor()
    {
        viewModel.StartNewCircuitDraft();
        highlightedHole = null;
        CircuitNameEntry.Text = viewModel.SuggestedCircuitName;
        CircuitDifficultyPicker.SelectedItem = null;
        CircuitInclinationEntry.Text = string.Empty;
        ApplyCircuitGlobalsToEditor(((App)Application.Current!).AppSettingsService.Load().CircuitDefaults);
        SetSelectedCircuitWallNames(
            viewModel.GetWallsForSelectedRoom()
                .Take(1)
                .Select(wall => wall.Name));
    }

    private void RefreshRoomAndWallPickers(CircuitPageState? pageState = null)
    {
        viewModel.EnsureSelectedRoom();

        pageState ??= pageStateService.Build(
            viewModel,
            interactionMode,
            specialModeHand,
            CircuitWallPicker.SelectedItem as WallDefinition);
        if (viewModel.SelectedCircuit is not null)
        {
            var circuitWallNames = viewModel.SelectedCircuit.GetWallNames();
            if (!selectedCircuitWallNames.SequenceEqual(circuitWallNames, StringComparer.Ordinal))
            {
                SetSelectedCircuitWallNames(circuitWallNames);
            }
        }

        isUpdatingWallControls = true;
        try
        {
        CircuitRoomPicker.ItemsSource = pageState.AvailableRooms.ToList();
        var selectedRoom = pageState.SelectedRoomName;
        if (!string.Equals(CircuitRoomPicker.SelectedItem as string, selectedRoom, StringComparison.Ordinal))
        {
            CircuitRoomPicker.SelectedItem = selectedRoom;
        }

        var roomWalls = pageState.VisibleWalls.ToList();
        if (selectedCircuitWallNames.Count == 0 && viewModel.SelectedCircuit is not null)
        {
            selectedCircuitWallNames.AddRange(viewModel.SelectedCircuit.GetWallNames());
        }

        selectedCircuitWallNames.RemoveAll(name =>
            roomWalls.All(wall => !string.Equals(wall.Name, name, StringComparison.Ordinal)));
        if (selectedCircuitWallNames.Count == 0 &&
            roomWalls.Count > 0 &&
            viewModel.SelectedCircuit is null)
        {
            selectedCircuitWallNames.Add(roomWalls[0].Name);
        }

        RebuildCircuitWallSelection(roomWalls);
        var selectedWalls = GetSelectedCircuitWalls();
        var currentSelection = CircuitWallPicker.SelectedItem as WallDefinition;
        var activeWall = currentSelection is not null &&
                         selectedWalls.Any(wall => wall.Id == currentSelection.Id)
            ? selectedWalls.First(wall => wall.Id == currentSelection.Id)
            : selectedWalls.FirstOrDefault(wall => wall.Id == viewModel.CurrentWall?.Id)
              ?? selectedWalls.FirstOrDefault();
        CircuitWallPicker.ItemsSource = selectedWalls.ToList();
        CircuitWallPicker.SelectedItem = activeWall;
        if (activeWall is not null)
        {
            viewModel.SetActiveWall(activeWall);
        }
        }
        finally
        {
            isUpdatingWallControls = false;
        }
    }

    private void RebuildCircuitWallSelection(IReadOnlyList<WallDefinition> roomWalls)
    {
        CircuitWallSelectionHost.Children.Clear();
        foreach (var wall in roomWalls)
        {
            var checkBox = new CheckBox
            {
                IsChecked = selectedCircuitWallNames.Contains(wall.Name, StringComparer.Ordinal),
                VerticalOptions = LayoutOptions.Center
            };
            checkBox.CheckedChanged += async (_, args) =>
                await OnCircuitWallMembershipChangedAsync(wall, args.Value, checkBox);

            var movementCount = viewModel.SelectedCircuit?.Movements.Count(movement =>
                string.Equals(movement.WallName, wall.Name, StringComparison.Ordinal)) ?? 0;
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8
            };
            row.Add(checkBox);
            row.Add(new Label
            {
                Text = wall.Name,
                TextColor = Color.FromArgb("#F8E7A8"),
                VerticalOptions = LayoutOptions.Center
            }, 1);
            row.Add(new Label
            {
                Text = movementCount == 1 ? "1 movimento" : $"{movementCount} movimenti",
                FontSize = 11,
                TextColor = Color.FromArgb("#B9AA79"),
                VerticalOptions = LayoutOptions.Center
            }, 2);
            CircuitWallSelectionHost.Children.Add(row);
        }
    }

    private async Task OnCircuitWallMembershipChangedAsync(
        WallDefinition wall,
        bool isSelected,
        CheckBox checkBox)
    {
        if (isUpdatingWallControls)
        {
            return;
        }

        var candidateNames = selectedCircuitWallNames.ToList();
        if (isSelected)
        {
            if (!candidateNames.Contains(wall.Name, StringComparer.Ordinal))
            {
                candidateNames.Add(wall.Name);
            }
        }
        else
        {
            candidateNames.RemoveAll(name => string.Equals(name, wall.Name, StringComparison.Ordinal));
        }

        if (candidateNames.Count == 0)
        {
            isUpdatingWallControls = true;
            checkBox.IsChecked = true;
            isUpdatingWallControls = false;
            await DisplayAlertAsync("Pareti circuito", "Il circuito deve mantenere almeno una parete.", "OK");
            return;
        }

        try
        {
            var candidateWalls = ResolveWalls(candidateNames);
            viewModel.SetSelectedCircuitWallsDraft(candidateWalls);
            SetSelectedCircuitWallNames(candidateNames);
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            isUpdatingWallControls = true;
            checkBox.IsChecked = !isSelected;
            isUpdatingWallControls = false;
            await DisplayAlertAsync("Pareti circuito", ex.Message, "OK");
        }
    }

    private IReadOnlyList<WallDefinition> GetSelectedCircuitWalls()
    {
        return ResolveWalls(selectedCircuitWallNames);
    }

    private IReadOnlyList<WallDefinition> ResolveWalls(IEnumerable<string> wallNames)
    {
        var wallsByName = viewModel.GetWallsForSelectedRoom()
            .ToDictionary(wall => wall.Name, StringComparer.Ordinal);
        return wallNames
            .Where(wallsByName.ContainsKey)
            .Select(name => wallsByName[name])
            .ToList();
    }

    private void SetSelectedCircuitWallNames(IEnumerable<string> wallNames)
    {
        selectedCircuitWallNames.Clear();
        selectedCircuitWallNames.AddRange(wallNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal));
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

        using var busy = AppBusy.Show("Salvataggio movimento...");
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

    private async Task ToggleFootHoldFromTapAsync(TappedEventArgs e)
    {
        var hole = FindTappedHole(e);
        if (hole is null)
        {
            return;
        }

        highlightedHole = hole;
        await ToggleHighlightedFootHoldAsync();
    }

    private async Task ToggleHighlightedFootHoldAsync()
    {
        if (highlightedHole is not WallHoleDefinition hole || hole.Number <= 0)
        {
            return;
        }

        try
        {
            await viewModel.ToggleFootHoldAsync(hole);
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

        using var busy = AppBusy.Show("Rimozione movimento...");
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
        SetModeVisual(FeetModeButton, interactionMode == CircuitInteractionMode.Feet);
        SetModeVisual(RemoveModeButton, interactionMode == CircuitInteractionMode.Remove);
        InteractionHintLabel.Text = pageStateService
            .Build(viewModel, interactionMode, specialModeHand, CircuitWallPicker.SelectedItem as WallDefinition)
            .InteractionHintText;
    }

    private static void SetModeVisual(Button button, bool isActive)
    {
        button.BackgroundColor = isActive ? Color.FromArgb("#F2C94C") : Color.FromArgb("#3A3120");
        button.TextColor = isActive ? Color.FromArgb("#14110B") : Color.FromArgb("#F8E7A8");
    }

    private void UpdateWallImageOverlay()
    {
        var wall = viewModel.CurrentWall;
        if (wall is null)
        {
            ClearPanelImageOverlay();
            return;
        }

        var wallKey = FormattableString.Invariant($"{wall.Id}:{wall.RoomName}:{wall.Name}");
        if (!string.Equals(panelPreviewWallKey, wallKey, StringComparison.Ordinal))
        {
            ClearPanelImageOverlay();
            panelPreviewWallKey = wallKey;
        }

        var wallBounds = previewDrawable.GetWallBounds();
        var scale = Math.Max(0.01f, previewDrawable.PixelsPerMillimeter * previewDrawable.ZoomFactor);
        var activePanelKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var panel in wall.Panels.Where(panel => !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)))
        {
            var imageTimestamp = File.GetLastWriteTimeUtc(panel.ImagePath!).Ticks;
            var panelKey = FormattableString.Invariant($"{panel.Name}:{panel.ImagePath}:{imageTimestamp}");
            activePanelKeys.Add(panelKey);
            var panelBaseX = wallBounds.X + ((float)panel.X * scale);
            var panelBaseY = wallBounds.Y + ((float)panel.Y * scale);
            var panelWidth = (float)panel.Width * scale;
            var panelHeight = (float)panel.Height * scale;
            Rect imageBounds;

            if (panel.IsImageRectified)
            {
                imageBounds = new Rect(panelBaseX, panelBaseY, panelWidth, panelHeight);
            }
            else
            {
                var imageWidth = panelWidth * (float)Math.Max(0.2d, panel.ImageScale);
                var imageHeight = panelHeight * (float)Math.Max(0.2d, panel.ImageScale);
                var stretchedWidth = imageWidth / (float)panel.EffectiveImageCropWidthFactor;
                var stretchedHeight = imageHeight / (float)panel.EffectiveImageCropHeightFactor;
                var imageX = panelBaseX + ((float)panel.ImageOffsetX * scale) -
                             (float)(panel.EffectiveImageCropLeft * stretchedWidth);
                var imageY = panelBaseY + ((float)panel.ImageOffsetY * scale) -
                             (float)(panel.EffectiveImageCropTop * stretchedHeight);
                imageBounds = new Rect(imageX, imageY, stretchedWidth, stretchedHeight);
            }

            if (!panelPreviewImages.TryGetValue(panelKey, out var image))
            {
                image = new Image
                {
                    Source = ImageSource.FromFile(panel.ImagePath!),
                    Aspect = Aspect.Fill,
                    InputTransparent = true
                };
                panelPreviewImages[panelKey] = image;
                CircuitPanelImagesHost.Children.Add(image);
            }

            image.Opacity = panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity;
            AbsoluteLayout.SetLayoutBounds(image, imageBounds);
            AbsoluteLayout.SetLayoutFlags(image, AbsoluteLayoutFlags.None);
        }

        foreach (var staleKey in panelPreviewImages.Keys.Where(key => !activePanelKeys.Contains(key)).ToList())
        {
            var image = panelPreviewImages[staleKey];
            image.Source = null;
            CircuitPanelImagesHost.Children.Remove(image);
            panelPreviewImages.Remove(staleKey);
        }
    }

    private void ClearPanelImageOverlay()
    {
        foreach (var image in panelPreviewImages.Values)
        {
            image.Source = null;
        }

        panelPreviewImages.Clear();
        panelPreviewWallKey = null;
        CircuitPanelImagesHost.Children.Clear();
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
                    Text = movement.IsFootHold
                        ? "Piedi - accesa insieme agli altri appoggi"
                        : $"{GetMovementRoleText(movement.Role)} {GetHandShortLabel(movement.Hand)} - Sequenza {movement.Sequence:00}",
                    TextColor = Color.FromArgb("#B9AA79"),
                    FontSize = 11
                }
            }
        }, 1);

        var card = new Border
        {
            Background = Color.FromArgb("#191611"),
            Stroke = GetMovementRoleColor(movement),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 10,
            Content = layout
        };
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => ShowMovementOnItsWall(movement))
        });
        return card;
    }

    private void ShowMovementOnItsWall(CircuitMovementDefinition movement)
    {
        var wall = ResolveMovementWall(movement.WallName);
        if (wall is null)
        {
            return;
        }

        viewModel.SetActiveWall(wall);
        CircuitWallPicker.SelectedItem = wall;
        var hole = wall.GetOrderedHoles().FirstOrDefault(item => item.Number == movement.HoleNumber);
        highlightedHole = hole.Number > 0 ? hole : null;
        SyncView();
    }

    private static View CreateMovementBadgeRow(CircuitMovementDefinition movement)
    {
        var roleColor = GetRoleColor(movement.Role);
        var roleText = GetMovementRoleBadgeText(movement.Role);
        var badges = new HorizontalStackLayout
        {
            Spacing = 8
        };

        if (!movement.IsFootHold)
        {
            badges.Children.Add(new Border
            {
                Background = GetHandColor(movement.Hand),
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
            });
            badges.Children.Add(new Border
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
            });
        }

        badges.Children.Add(new Border
        {
            Background = roleColor,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(8, 2),
            Content = new Label
            {
                Text = roleText,
                FontSize = 11,
                TextColor = movement.Role is MovementRole.Top or MovementRole.Feet
                    ? Color.FromArgb("#14110B")
                    : Color.FromArgb("#F8E7A8"),
                FontFamily = "OpenSansSemibold"
            }
        });

        return badges;
    }

    private static Color GetMovementRoleColor(CircuitMovementDefinition movement)
    {
        return movement.Role switch
        {
            MovementRole.Start => Color.FromArgb("#2E8B57"),
            MovementRole.Top => Color.FromArgb("#F2C94C"),
            MovementRole.Feet => Color.FromArgb("#7FDBFF"),
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
        QuickFeetButton.IsEnabled = canEditHighlightedHole;
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
                MovementRole.Feet => "Piedi",
                _ => "Mov"
            };

            if (movement.IsFootHold)
            {
                return roleLabel;
            }

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
            !movement.IsFootHold &&
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
        var wall = ResolveMovementWall(movement.WallName);
        if (wall is null)
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
        var image = new Image
        {
            Aspect = Aspect.AspectFill,
            WidthRequest = thumbnailSize,
            HeightRequest = thumbnailSize,
            BackgroundColor = Color.FromArgb("#241F16")
        };
        if (string.Equals(viewModel.CurrentWall?.Name, movement.WallName, StringComparison.Ordinal))
        {
            var thumbnailKey = BuildMovementThumbnailKey(wall, hole, panel);
            _ = LoadMovementThumbnailIntoImageAsync(
                image,
                thumbnailKey,
                wall,
                hole,
                thumbnailSize,
                movementThumbnailCancellation.Token);
        }

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

    private async Task LoadMovementThumbnailIntoImageAsync(
        Image image,
        string thumbnailKey,
        WallDefinition wall,
        WallHoleDefinition hole,
        double thumbnailSize,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!movementThumbnailTasks.TryGetValue(thumbnailKey, out var thumbnailTask))
            {
                thumbnailTask = LoadMovementThumbnailSourceAsync(
                    wall,
                    hole,
                    thumbnailSize,
                    cancellationToken);
                movementThumbnailTasks[thumbnailKey] = thumbnailTask;
            }

            var source = await thumbnailTask;
            if (source is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    image.Source = source;
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // The placeholder remains visible if a source image cannot be decoded.
        }
    }

    private async Task<ImageSource?> LoadMovementThumbnailSourceAsync(
        WallDefinition wall,
        WallHoleDefinition hole,
        double thumbnailSize,
        CancellationToken cancellationToken)
    {
        await movementThumbnailSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return TryCreateMovementThumbnailSource(wall, hole, thumbnailSize);
                },
                cancellationToken);
        }
        finally
        {
            movementThumbnailSemaphore.Release();
        }
    }

    private static string BuildMovementThumbnailKey(
        WallDefinition wall,
        WallHoleDefinition hole,
        PanelDefinition panel)
    {
        var imageTimestamp = File.GetLastWriteTimeUtc(panel.ImagePath!).Ticks;
        return FormattableString.Invariant(
            $"{wall.Id}:{hole.Number}:{panel.ImagePath}:{imageTimestamp}:{panel.ImageOffsetX}:{panel.ImageOffsetY}:{panel.ImageScale}:{panel.ImageCropLeft}:{panel.ImageCropTop}:{panel.ImageCropRight}:{panel.ImageCropBottom}");
    }

    private void ResetMovementThumbnailQueue()
    {
        movementThumbnailCancellation.Cancel();
        movementThumbnailCancellation.Dispose();
        movementThumbnailCancellation = new CancellationTokenSource();
        movementThumbnailTasks.Clear();
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
        return movement.IsFootHold
            ? $"Piedi - Foro {movement.HoleNumber}"
            : $"{GetMovementRoleText(movement.Role)} {GetHandShortLabel(movement.Hand)} - Foro {movement.HoleNumber}";
    }

    private View BuildMovementMetadataLabel(CircuitMovementDefinition movement)
    {
        var wall = ResolveMovementWall(movement.WallName);
        if (wall is null)
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

    private IReadOnlyList<WallHoleDefinition> GetCurrentStateHoles()
    {
        var currentHoles = new[]
        {
            ResolveCurrentHandStateHole(HandSide.Left),
            ResolveCurrentHandStateHole(HandSide.Right)
        }
        .Where(hole => hole.HasValue && hole.Value.Number > 0)
        .Select(hole => hole!.Value)
        .Concat(ResolveCircuitFootHoles())
        .GroupBy(hole => hole.Number)
        .Select(group => group.First())
        .ToList();

        return currentHoles;
    }

    private IReadOnlyList<WallHoleDefinition> ResolveCircuitFootHoles()
    {
        var circuit = viewModel.SelectedCircuit;
        var wall = viewModel.CurrentWall;
        if (circuit is null || wall is null)
        {
            return Array.Empty<WallHoleDefinition>();
        }

        var holesByNumber = wall.GetOrderedHoles().ToDictionary(hole => hole.Number);
        return circuit.Movements
            .Where(movement =>
                movement.IsFootHold &&
                string.Equals(movement.WallName, wall.Name, StringComparison.Ordinal))
            .OrderBy(movement => movement.HoleNumber)
            .Select(movement => holesByNumber.GetValueOrDefault(movement.HoleNumber))
            .Where(hole => hole.Number > 0)
            .ToList();
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

    private static WallHoleDefinition? ResolveHoleFromCircuit(
        CircuitDefinition circuit,
        WallDefinition wall,
        IReadOnlyList<WallHoleDefinition> holes,
        HandSide hand,
        MovementRole role)
    {
        var movement = circuit.Movements
            .Where(item =>
                string.Equals(item.WallName, wall.Name, StringComparison.Ordinal) &&
                item.Hand == hand &&
                item.Role == role)
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
            .Where(item =>
                item.Hand == hand &&
                item.Role != MovementRole.Top &&
                !item.IsFootHold)
            .OrderBy(item => item.Sequence)
            .LastOrDefault();

        if (lastHandMovement is not null &&
            string.Equals(lastHandMovement.WallName, wall.Name, StringComparison.Ordinal))
        {
            var lastHole = holes.FirstOrDefault(item => item.Number == lastHandMovement.HoleNumber);
            if (lastHole.Number > 0)
            {
                return lastHole;
            }
        }

        return lastHandMovement is null
            ? ResolveHoleFromCircuit(circuit, wall, holes, hand, MovementRole.Start)
            : null;
    }

    private WallDefinition? ResolveMovementWall(string wallName)
    {
        var circuit = viewModel.SelectedCircuit;
        if (circuit is null)
        {
            return null;
        }

        return viewModel.AvailableWalls.FirstOrDefault(wall =>
            string.Equals(wall.RoomName, circuit.RoomName, StringComparison.Ordinal) &&
            string.Equals(wall.Name, wallName, StringComparison.Ordinal));
    }

    private static string GetMovementRoleText(MovementRole role)
    {
        return role switch
        {
            MovementRole.Start => "Start",
            MovementRole.Top => "Top",
            MovementRole.Feet => "Piedi",
            _ => "Movimento"
        };
    }

    private static string GetMovementRoleBadgeText(MovementRole role)
    {
        return role switch
        {
            MovementRole.Start => "START",
            MovementRole.Top => "TOP",
            MovementRole.Feet => "PIEDI",
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
            MovementRole.Feet => Color.FromArgb("#7FDBFF"),
            _ => Color.FromArgb("#3A3120")
        };
    }
}
