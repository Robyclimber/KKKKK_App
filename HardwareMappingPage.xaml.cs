using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using RouteLab.Drawing;
using RouteLab.Models;
using RouteLab.ViewModels;

namespace RouteLab;

public partial class HardwareMappingPage : ContentPage
{
    private sealed class HoleMappingEditor
    {
        public required Border CardBorder { get; init; }

        public required Label ConflictLabel { get; init; }

        public required Entry PointIdEntry { get; init; }

        public required Entry LedIndexEntry { get; init; }

        public required Switch EnabledSwitch { get; init; }
    }

    private sealed class HoleMappingConflictState
    {
        public required HashSet<int> DuplicateLedIndices { get; init; }

        public required HashSet<string> DuplicatePointIds { get; init; }
    }

    private const int HoleMappingBatchSize = 40;

    private readonly GymSetupViewModel viewModel;
    private readonly IReadOnlyList<LedStartDirection> availableWallLedDirections =
        [LedStartDirection.TopToBottom, LedStartDirection.BottomToTop];
    private readonly List<HoleMappingEditor> holeMappingEditors = new();
    private readonly CircuitEditorDrawable manualOrderDrawable = new();
    private readonly Dictionary<string, Image> manualOrderPanelImages = new(StringComparer.Ordinal);
    private bool isSyncingSelection;
    private bool showOnlyHoleMappingConflicts;
    private bool showOnlyManualOrder;
    private int visibleHoleMappingCount = HoleMappingBatchSize;
    private double manualOrderZoom = 1d;
    private double manualOrderZoomStart = 1d;
    private double manualOrderBaseWidth = 320d;
    private double manualOrderBaseHeight = 320d;
    private WallHoleDefinition? selectedManualOrderHole;

    public HardwareMappingPage()
    {
        InitializeComponent();
        viewModel = ((App)Application.Current!).GymSetupViewModel;
        RoomsPicker.ItemsSource = viewModel.Rooms;
        WallLedDirectionPicker.ItemsSource = availableWallLedDirections
            .Select(GetWallLedDirectionLabel)
            .ToList();
        Loaded += OnPageLoaded;
        ManualOrderCanvas.Drawable = manualOrderDrawable;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;
        using var busy = AppBusy.Show("Caricamento mapping...");
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var selectedRoomName = viewModel.SelectedRoom?.Name;
            var selectedWallName = viewModel.SelectedWall?.Name;
            await viewModel.EnsureLoadedAsync();

            if (!string.IsNullOrWhiteSpace(selectedRoomName))
            {
                var matchingRoom = viewModel.Rooms.FirstOrDefault(room => string.Equals(room.Name, selectedRoomName, StringComparison.Ordinal));
                if (matchingRoom is not null)
                {
                    viewModel.SelectRoom(matchingRoom);
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedWallName))
            {
                var matchingWall = viewModel.Walls.FirstOrDefault(wall =>
                    string.Equals(wall.Name, selectedWallName, StringComparison.Ordinal) &&
                    string.Equals(wall.RoomName, viewModel.SelectedRoom?.Name, StringComparison.Ordinal));
                if (matchingWall is not null)
                {
                    viewModel.SelectWall(matchingWall);
                }
            }

            SyncViewFromState();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Mapping hardware della parete", $"Errore inizializzazione mapping hardware della parete: {ex.Message}", "OK");
        }
    }

    private void SyncViewFromState()
    {
        isSyncingSelection = true;
        RoomsPicker.ItemsSource = null;
        RoomsPicker.ItemsSource = viewModel.Rooms;
        RoomsPicker.SelectedItem = viewModel.SelectedRoom;
        WallsPicker.ItemsSource = viewModel.GetWallsForSelectedRoom().ToList();
        WallsPicker.SelectedItem = viewModel.SelectedWall;
        WallLedDirectionPicker.SelectedIndex = viewModel.SelectedWall is null
            ? -1
            : GetWallLedDirectionIndex(viewModel.SelectedWall.LedVerticalDirection);
        WallLedDirectionPicker.IsEnabled = viewModel.SelectedWall is not null;
        isSyncingSelection = false;

        var wall = viewModel.SelectedWall;
        WallInfoLabel.Text = wall is null
            ? "Nessuna parete selezionata."
            : $"{wall.DisplayLabel} - fori: {wall.GetOrderedHoles().Count}";
        SaveToolbarItem.IsEnabled = wall is not null;
        manualOrderDrawable.Wall = wall;
        manualOrderDrawable.HighlightedHole = selectedManualOrderHole;
        manualOrderDrawable.SelectedHoles = Array.Empty<WallHoleDefinition>();
        UpdateManualOrderPreview();
        RebuildHoleMappingsList();
    }

    private void OnRoomSelectionChanged(object? sender, EventArgs e)
    {
        if (isSyncingSelection)
        {
            return;
        }

        viewModel.SelectRoom(RoomsPicker.SelectedItem as RoomDefinition);
        visibleHoleMappingCount = HoleMappingBatchSize;
        SyncViewFromState();
    }

    private void OnWallSelectionChanged(object? sender, EventArgs e)
    {
        if (isSyncingSelection)
        {
            return;
        }

        viewModel.SelectWall(WallsPicker.SelectedItem as WallDefinition);
        visibleHoleMappingCount = HoleMappingBatchSize;
        SyncViewFromState();
    }

    private void RebuildHoleMappingsList()
    {
        holeMappingEditors.Clear();
        HoleMappingsHost.Children.Clear();
        HoleMappingSummaryLabel.Text = string.Empty;
        ManualOrderStatusLabel.Text = string.Empty;
        LoadMoreHoleMappingsButton.IsVisible = false;

        var wall = viewModel.SelectedWall;
        if (wall is null)
        {
            HoleMappingEmptyLabel.Text = "Seleziona una parete.";
            HoleMappingEmptyLabel.IsVisible = true;
            return;
        }

        var orderedHoles = wall.GetOrderedHoles();
        var manualCount = orderedHoles.Count(hole => hole.ManualOrder > 0);
        ManualOrderStatusLabel.Text = manualCount == 0
            ? "1. Premi Azzera e ricomincia. 2. Sui fori, premi Assegna #1, #2, #3 nell'ordine fisico desiderato."
            : $"Ordine manuale: {manualCount} / {orderedHoles.Count} fori assegnati. Prossimo numero: {wall.GetNextManualOrder()}.";
        var conflictState = BuildHoleMappingConflictState(orderedHoles);
        if (orderedHoles.Count == 0)
        {
            HoleMappingEmptyLabel.Text = "Nessun foro disponibile.";
            HoleMappingEmptyLabel.IsVisible = true;
            return;
        }

        var holesToDisplay = showOnlyManualOrder
            ? orderedHoles.Where(hole => hole.ManualOrder > 0).OrderBy(hole => hole.ManualOrder).ToList()
            : showOnlyHoleMappingConflicts
            ? orderedHoles.Where(hole => HasHoleMappingConflict(hole, conflictState)).ToList()
            : orderedHoles;

        if (holesToDisplay.Count == 0)
        {
            HoleMappingEmptyLabel.Text = "Nessun conflitto hardware.";
            HoleMappingEmptyLabel.IsVisible = true;
            return;
        }

        HoleMappingEmptyLabel.IsVisible = false;
        var displayCount = Math.Min(visibleHoleMappingCount, holesToDisplay.Count);
        HoleMappingSummaryLabel.Text = $"Fori mostrati: {displayCount} / {holesToDisplay.Count}";

        foreach (var hole in holesToDisplay.Take(displayCount))
        {
            HoleMappingsHost.Children.Add(CreateHoleMappingCard(hole));
        }

        LoadMoreHoleMappingsButton.IsVisible = displayCount < holesToDisplay.Count;
        RefreshHoleMappingValidationState();
    }

    private View CreateHoleMappingCard(WallHoleDefinition hole)
    {
        var wall = viewModel.SelectedWall!;
        var pointIdEntry = new Entry
        {
            Text = hole.PointId,
            Placeholder = $"pointId foro {hole.Number}"
        };

        var ledIndexEntry = new Entry
        {
            Text = hole.LedIndex.ToString(CultureInfo.InvariantCulture),
            Keyboard = Keyboard.Numeric,
            IsReadOnly = true,
            WidthRequest = 90
        };

        var enabledSwitch = new Switch
        {
            IsToggled = hole.IsEnabled,
            OnColor = Color.FromArgb("#F2C94C")
        };

        async Task ApplyAsync()
        {
            try
            {
                var ledIndex = ParsePositiveDouble(ledIndexEntry.Text, "Inserisci un indice LED valido.");
                viewModel.UpdateHoleHardware(hole.Number, pointIdEntry.Text, (int)Math.Round(ledIndex), enabledSwitch.IsToggled);
                await viewModel.SaveSelectedWallAsync();
                SyncViewFromState();
            }
            catch (InvalidOperationException ex)
            {
                await DisplayAlertAsync("Mapping hardware della parete", ex.Message, "OK");
            }
        }

        pointIdEntry.TextChanged += (_, _) => RefreshHoleMappingValidationState();
        ledIndexEntry.TextChanged += (_, _) => RefreshHoleMappingValidationState();
        enabledSwitch.Toggled += async (_, _) =>
        {
            RefreshHoleMappingValidationState();
            await ApplyAsync();
        };
        pointIdEntry.Completed += async (_, _) => await ApplyAsync();
        pointIdEntry.Unfocused += async (_, _) => await ApplyAsync();
        var assignManualOrderButton = new Button
        {
            Text = hole.ManualOrder > 0 ? $"Ordine manuale: {hole.ManualOrder}" : $"Assegna #{wall.GetNextManualOrder()}",
            Style = (Style)Application.Current!.Resources["SecondaryActionButtonStyle"]!
        };
        assignManualOrderButton.Clicked += async (_, _) =>
        {
            try
            {
                viewModel.SetManualHoleOrder(hole.Number, wall.GetNextManualOrder());
                await viewModel.SaveSelectedWallAsync();
                visibleHoleMappingCount = Math.Max(visibleHoleMappingCount, HoleMappingBatchSize);
                SyncViewFromState();
            }
            catch (InvalidOperationException ex)
            {
                await DisplayAlertAsync("Ordine manuale", ex.Message, "OK");
            }
        };
        var hardwareGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };
        hardwareGrid.Add(new Label
        {
            Text = "LED",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#D8A72D")
        }, 0, 0);
        hardwareGrid.Add(ledIndexEntry, 1, 0);
        hardwareGrid.Add(new Label
        {
            Text = "Funzionante",
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            TextColor = Color.FromArgb("#D8A72D")
        }, 2, 0);
        hardwareGrid.Add(enabledSwitch, 3, 0);

        var conflictLabel = new Label
        {
            Text = "Nessun conflitto hardware.",
            FontSize = 12,
            TextColor = Color.FromArgb("#6FAF7B")
        };

        var border = new Border
        {
            Background = Color.FromArgb("#191611"),
            Stroke = Color.FromArgb("#B9922F"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 10,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = $"Foro {hole.Number} - {hole.PanelName}{(hole.IsEnabled ? string.Empty : " - GUASTO")}",
                        FontSize = 15,
                        TextColor = Color.FromArgb("#F8E7A8")
                    },
                    new Label
                    {
                        Text = $"X {hole.AbsoluteX:0.#} - Y {hole.AbsoluteY:0.#}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#B9AA79")
                    },
                    conflictLabel,
                    pointIdEntry,
                    hardwareGrid,
                    assignManualOrderButton
                }
            }
        };

        holeMappingEditors.Add(new HoleMappingEditor
        {
            CardBorder = border,
            ConflictLabel = conflictLabel,
            PointIdEntry = pointIdEntry,
            LedIndexEntry = ledIndexEntry,
            EnabledSwitch = enabledSwitch
        });

        return border;
    }

    private void RefreshHoleMappingValidationState()
    {
        var conflictState = BuildHoleMappingConflictState(viewModel.SelectedWall?.GetOrderedHoles());

        foreach (var editor in holeMappingEditors)
        {
            var conflictMessages = new List<string>();
            var parsedLedIndex = TryParsePositiveInt(editor.LedIndexEntry.Text);
            if (editor.EnabledSwitch.IsToggled && parsedLedIndex.HasValue && conflictState.DuplicateLedIndices.Contains(parsedLedIndex.Value))
            {
                conflictMessages.Add($"LED {parsedLedIndex.Value} duplicato");
            }

            var pointId = editor.PointIdEntry.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(pointId) && conflictState.DuplicatePointIds.Contains(pointId))
            {
                conflictMessages.Add("pointId duplicato");
            }

            var hasConflict = conflictMessages.Count > 0;
            editor.CardBorder.Background = hasConflict ? Color.FromArgb("#2A1616") : Color.FromArgb("#191611");
            editor.CardBorder.Stroke = hasConflict ? Color.FromArgb("#E05A47") : Color.FromArgb("#B9922F");
            editor.CardBorder.StrokeThickness = hasConflict ? 2 : 1;
            editor.ConflictLabel.Text = hasConflict
                ? string.Join(" | ", conflictMessages)
                : "Nessun conflitto hardware.";
            editor.ConflictLabel.TextColor = hasConflict ? Color.FromArgb("#F08A7E") : Color.FromArgb("#6FAF7B");
        }
    }

    private HoleMappingConflictState BuildHoleMappingConflictState(IReadOnlyList<WallHoleDefinition>? holes)
    {
        holes ??= Array.Empty<WallHoleDefinition>();

        var duplicateLedIndices = holes
            .Where(hole => hole.IsEnabled && hole.LedIndex > 0)
            .GroupBy(hole => hole.LedIndex)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        var duplicatePointIds = holes
            .Select(hole => hole.PointId?.Trim())
            .Where(pointId => !string.IsNullOrWhiteSpace(pointId))
            .GroupBy(pointId => pointId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new HoleMappingConflictState
        {
            DuplicateLedIndices = duplicateLedIndices,
            DuplicatePointIds = duplicatePointIds
        };
    }

    private static bool HasHoleMappingConflict(WallHoleDefinition hole, HoleMappingConflictState conflictState)
    {
        var hasDuplicateLed = hole.IsEnabled && hole.LedIndex > 0 && conflictState.DuplicateLedIndices.Contains(hole.LedIndex);
        var pointId = hole.PointId?.Trim();
        var hasDuplicatePointId = !string.IsNullOrWhiteSpace(pointId) && conflictState.DuplicatePointIds.Contains(pointId);
        return hasDuplicateLed || hasDuplicatePointId;
    }

    private void OnShowOnlyHoleConflictsToggled(object? sender, ToggledEventArgs e)
    {
        showOnlyHoleMappingConflicts = e.Value;
        visibleHoleMappingCount = HoleMappingBatchSize;
        RebuildHoleMappingsList();
    }

    private async void OnClearManualOrderClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedWall is null) return;
        var confirmed = await DisplayAlertAsync("Ordine manuale", "Vuoi cancellare l'ordine manuale e ricominciare da 1?", "Sì", "No");
        if (!confirmed) return;
        viewModel.ClearManualHoleOrder();
        await viewModel.SaveSelectedWallAsync();
        showOnlyManualOrder = false;
        SyncViewFromState();
    }

    private void OnShowManualOrderClicked(object? sender, EventArgs e)
    {
        showOnlyManualOrder = !showOnlyManualOrder;
        visibleHoleMappingCount = HoleMappingBatchSize;
        RebuildHoleMappingsList();
    }

    private void OnAutoMapHoleLedsClicked(object? sender, EventArgs e)
    {
        var wall = viewModel.SelectedWall;
        if (wall is null)
        {
            return;
        }

        wall.LedVerticalDirection = GetSelectedWallLedDirection();
        wall.AutoAssignLedIndicesByWallRouting();
        SyncViewFromState();
    }

    private void OnWallLedDirectionChanged(object? sender, EventArgs e)
    {
        if (isSyncingSelection || viewModel.SelectedWall is not { } wall)
        {
            return;
        }

        wall.LedVerticalDirection = GetSelectedWallLedDirection();
        wall.AutoAssignLedIndicesByWallRouting();
        visibleHoleMappingCount = HoleMappingBatchSize;
        RebuildHoleMappingsList();
    }

    private void OnLoadMoreHoleMappingsClicked(object? sender, EventArgs e)
    {
        visibleHoleMappingCount += HoleMappingBatchSize;
        RebuildHoleMappingsList();
    }

    private async void OnSaveWallClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Salvataggio mapping...");
        try
        {
            var result = await viewModel.SaveSelectedWallAsync();
            await DisplayAlertAsync("Salvataggio completato", $"Parete salvata su database.\n{result}", "OK");
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Mapping hardware della parete", ex.Message, "OK");
        }
    }

    private void OnManualOrderViewportSizeChanged(object? sender, EventArgs e)
    {
        manualOrderBaseWidth = Math.Max(280d, ManualOrderViewport.Width - 4d);
        manualOrderBaseHeight = Math.Max(280d, ManualOrderViewport.Height - 4d);
        UpdateManualOrderPreview();
    }

    private void OnOpenManualOrderImageClicked(object? sender, EventArgs e)
    {
        OpenManualOrderImageButton.IsVisible = false;
        ManualOrderImageControls.IsVisible = true;
        ManualOrderViewport.IsVisible = true;
        ManualOrderSelectedHoleLabel.IsVisible = true;
        Dispatcher.Dispatch(UpdateManualOrderPreview);
    }

    private void OnManualOrderZoomInClicked(object? sender, EventArgs e)
    {
        manualOrderZoom = Math.Clamp(manualOrderZoom + 0.25d, 1d, 4d);
        manualOrderZoomStart = manualOrderZoom;
        UpdateManualOrderPreview();
    }

    private void OnManualOrderZoomOutClicked(object? sender, EventArgs e)
    {
        manualOrderZoom = Math.Clamp(manualOrderZoom - 0.25d, 1d, 4d);
        manualOrderZoomStart = manualOrderZoom;
        UpdateManualOrderPreview();
    }

    private void OnManualOrderZoomResetClicked(object? sender, EventArgs e)
    {
        manualOrderZoom = 1d;
        manualOrderZoomStart = manualOrderZoom;
        UpdateManualOrderPreview();
    }

    private void OnManualOrderPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            manualOrderZoomStart = manualOrderZoom;
        }
        else if (e.Status == GestureStatus.Running)
        {
            manualOrderZoom = Math.Clamp(manualOrderZoomStart * e.Scale, 1d, 4d);
            UpdateManualOrderPreview();
        }
    }

    private async void OnManualOrderHoleTapped(object? sender, TappedEventArgs e)
    {
        var position = e.GetPosition(ManualOrderCanvas);
        var hole = position is null ? null : manualOrderDrawable.FindNearestHole(position.Value, 30d);
        if (hole is null || viewModel.SelectedWall is null)
        {
            return;
        }

        try
        {
            var nextOrder = viewModel.SelectedWall.GetNextManualOrder();
            viewModel.SetManualHoleOrder(hole.Value.Number, nextOrder);
            await viewModel.SaveSelectedWallAsync();
            selectedManualOrderHole = viewModel.SelectedWall.GetOrderedHoles().FirstOrDefault(item => item.ManualOrder == nextOrder);
            ManualOrderSelectedHoleLabel.Text = $"Foro assegnato come #{nextOrder}. Tocca ora il foro #{nextOrder + 1}.";
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Ordine manuale", ex.Message, "OK");
        }
    }

    private void UpdateManualOrderPreview()
    {
        var wall = viewModel.SelectedWall;
        if (wall is null || !ManualOrderViewport.IsVisible || !ManualOrderCanvas.IsLoaded)
        {
            return;
        }

        var padding = 48d;
        var availableWidth = Math.Max(1d, manualOrderBaseWidth - padding);
        var availableHeight = Math.Max(1d, manualOrderBaseHeight - padding);
        manualOrderDrawable.PixelsPerMillimeter = (float)Math.Max(0.01d, Math.Min(availableWidth / wall.Width, availableHeight / wall.Height));
        manualOrderDrawable.ZoomFactor = (float)manualOrderZoom;
        var desiredSize = manualOrderDrawable.GetDesiredSize(manualOrderZoom);
        ManualOrderCanvas.WidthRequest = Math.Max(manualOrderBaseWidth, desiredSize.Width);
        ManualOrderCanvas.HeightRequest = Math.Max(manualOrderBaseHeight, desiredSize.Height);
        ManualOrderLayer.WidthRequest = ManualOrderCanvas.WidthRequest;
        ManualOrderLayer.HeightRequest = ManualOrderCanvas.HeightRequest;
        UpdateManualOrderImages();
        ManualOrderCanvas.Invalidate();
    }

    private void UpdateManualOrderImages()
    {
        var wall = viewModel.SelectedWall;
        if (wall is null) return;

        var wallBounds = manualOrderDrawable.GetWallBounds();
        var scale = Math.Max(0.01f, manualOrderDrawable.PixelsPerMillimeter * manualOrderDrawable.ZoomFactor);
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var panel in wall.Panels.Where(panel => !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)))
        {
            var key = FormattableString.Invariant($"{panel.Name}:{panel.ImagePath}:{File.GetLastWriteTimeUtc(panel.ImagePath!).Ticks}");
            activeKeys.Add(key);
            if (!manualOrderPanelImages.TryGetValue(key, out var image))
            {
                image = new Image { Source = ImageSource.FromFile(panel.ImagePath!), Aspect = Aspect.Fill, InputTransparent = true };
                manualOrderPanelImages[key] = image;
                ManualOrderPanelImagesHost.Children.Add(image);
            }

            var x = wallBounds.X + ((float)panel.X * scale);
            var y = wallBounds.Y + ((float)panel.Y * scale);
            var width = (float)panel.Width * scale;
            var height = (float)panel.Height * scale;
            image.Opacity = panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity;
            AbsoluteLayout.SetLayoutBounds(image, new Rect(x, y, width, height));
            AbsoluteLayout.SetLayoutFlags(image, AbsoluteLayoutFlags.None);
        }

        foreach (var key in manualOrderPanelImages.Keys.Where(key => !activeKeys.Contains(key)).ToList())
        {
            var image = manualOrderPanelImages[key];
            image.Source = null;
            ManualOrderPanelImagesHost.Children.Remove(image);
            manualOrderPanelImages.Remove(key);
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Aggiornamento mapping...");
        await InitializeAsync();
    }

    private static double ParsePositiveDouble(string? text, string errorMessage)
    {
        var normalized = text?.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value;
    }

    private static int? TryParsePositiveInt(string? text)
    {
        return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
    }

    private LedStartDirection GetSelectedWallLedDirection()
    {
        return WallLedDirectionPicker.SelectedIndex >= 0 &&
               WallLedDirectionPicker.SelectedIndex < availableWallLedDirections.Count
            ? availableWallLedDirections[WallLedDirectionPicker.SelectedIndex]
            : LedStartDirection.TopToBottom;
    }

    private static string GetWallLedDirectionLabel(LedStartDirection direction)
    {
        return direction == LedStartDirection.BottomToTop
            ? "Dal basso verso l'alto"
            : "Dall'alto verso il basso";
    }

    private int GetWallLedDirectionIndex(LedStartDirection direction)
    {
        return availableWallLedDirections
            .Select((value, index) => (value, index))
            .FirstOrDefault(item => item.value == direction)
            .index;
    }
}
