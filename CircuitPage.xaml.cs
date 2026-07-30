using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using WallPanelPlanner.Drawing;
using WallPanelPlanner.Models;
using WallPanelPlanner.ViewModels;

namespace WallPanelPlanner;

public partial class CircuitPage : ContentPage
{
    private readonly Services.ICircuitPageStateService pageStateService;
    private readonly CircuitEditorViewModel viewModel;
    private readonly CircuitEditorDrawable previewDrawable = new();
    private double previewZoom = 1d;
    private double previewZoomStart = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;
    private CircuitInteractionMode interactionMode = CircuitInteractionMode.RightHand;
    private HandSide specialModeHand = HandSide.Right;
    private WallHoleDefinition? highlightedHole;
    private bool isRefreshing;

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

            CircuitNameEntry.Text = viewModel.SuggestedCircuitName;
            SyncView();
        }
        catch (Exception ex)
        {
            var databaseFactory = new Persistence.SqliteDatabaseFactory();
            var wallRepository = new Services.SqliteWallRepository(databaseFactory);
            var roomRepository = new Services.SqliteRoomRepository(databaseFactory);
            pageStateService = new Services.CircuitPageStateService();
            viewModel = new ViewModels.CircuitEditorViewModel(
                new Services.CircuitEditingService(),
                new ViewModels.GymSetupViewModel(
                    new Services.GymSetupService(),
                    new Services.WallConfigurationStorageService(wallRepository),
                    wallRepository,
                    roomRepository),
                new Services.SqliteCircuitRepository(databaseFactory));
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

    private async void OnCreateCircuitClicked(object? sender, EventArgs e)
    {
        try
        {
            await viewModel.CreateCircuitAsync(
                CircuitNameEntry.Text,
                CircuitDifficultyEntry.Text,
                CircuitInclinationEntry.Text,
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
            await viewModel.UpdateSelectedCircuitAsync(CircuitNameEntry.Text, CircuitDifficultyEntry.Text, CircuitInclinationEntry.Text);
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

    private void OnCircuitRoomChanged(object? sender, EventArgs e)
    {
        highlightedHole = null;
        viewModel.SetSelectedRoom(CircuitRoomPicker.SelectedItem as string);
        RefreshRoomAndWallPickers();
        LoadCircuitIntoEditor(viewModel.SelectedCircuit);
        SyncView();
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

    private void OnPreviewSingleTapped(object? sender, TappedEventArgs e)
    {
        switch (interactionMode)
        {
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
            default:
                ToggleHoleForHand(e, HandSide.Right, MovementRole.Normal);
                break;
        }
    }

    private void OnPreviewDoubleTappedForLeftHand(object? sender, TappedEventArgs e)
    {
        ToggleHoleForHand(e, HandSide.Left, MovementRole.Normal);
    }

    private void OnPreviewTripleTappedToRemove(object? sender, TappedEventArgs e)
    {
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
        UpdateCircuitButton.IsEnabled = pageState.CanUpdateCircuit;
        DeleteCircuitButton.IsEnabled = pageState.CanDeleteCircuit;
        CircuitWallPicker.IsEnabled = pageState.CanPickWall;
        previewDrawable.Wall = viewModel.CurrentWall;
        previewDrawable.Circuit = viewModel.SelectedCircuit;
        previewDrawable.HighlightedHole = highlightedHole;
        SelectedHoleInfoLabel.Text = BuildSelectedHoleInfoText();
        UpdateHighlightedHoleActions();
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
        CircuitDifficultyEntry.Text = circuit.Difficulty;
        CircuitInclinationEntry.Text = circuit.Inclination;
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
        CircuitDifficultyEntry.Text = string.Empty;
        CircuitInclinationEntry.Text = string.Empty;
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

    private void UpdateInteractionButtons()
    {
        SetModeVisual(RightHandModeButton, interactionMode == CircuitInteractionMode.RightHand);
        SetModeVisual(LeftHandModeButton, interactionMode == CircuitInteractionMode.LeftHand);
        SetModeVisual(StartModeButton, interactionMode == CircuitInteractionMode.Start);
        SetModeVisual(TopModeButton, interactionMode == CircuitInteractionMode.Top);
        SetModeVisual(RemoveModeButton, interactionMode == CircuitInteractionMode.Remove);
        InteractionHintLabel.Text = pageStateService.Build(viewModel, interactionMode, specialModeHand, CircuitWallPicker.SelectedItem as WallDefinition).InteractionHintText;
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
            var imageX = panelBaseX + ((float)panel.ImageOffsetX * scale);
            var imageY = panelBaseY + ((float)panel.ImageOffsetY * scale);

            var image = new Image
            {
                Source = ImageSource.FromFile(panel.ImagePath!),
                Opacity = panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity,
                Aspect = Aspect.Fill,
                InputTransparent = true
            };

            AbsoluteLayout.SetLayoutBounds(image, new Rect(imageX, imageY, imageWidth, imageHeight));
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
            return $"Foro {hole.Number} - pannello {hole.PanelName} - X {hole.AbsoluteX:0.#} mm - Y {hole.AbsoluteY:0.#} mm";
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

        return $"Foro {hole.Number} - {states} - pannello {hole.PanelName}";
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
        var cropLeftPx = sourceWidth * panel.EffectiveImageCropLeft;
        var cropTopPx = sourceHeight * panel.EffectiveImageCropTop;
        var cropWidthPx = sourceWidth * panel.EffectiveImageCropWidthFactor;
        var cropHeightPx = sourceHeight * panel.EffectiveImageCropHeightFactor;
        var imageScale = Math.Max(0.2d, panel.ImageScale);
        var overlayWidth = Math.Max(1d, panel.Width * imageScale);
        var overlayHeight = Math.Max(1d, panel.Height * imageScale);
        var holeOverlayX = hole.RelativeX - panel.ImageOffsetX;
        var holeOverlayY = hole.RelativeY - panel.ImageOffsetY;
        var sourceHoleX = cropLeftPx + ((holeOverlayX / overlayWidth) * cropWidthPx);
        var sourceHoleY = cropTopPx + ((holeOverlayY / overlayHeight) * cropHeightPx);

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
