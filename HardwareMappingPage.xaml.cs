using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using WallPanelPlanner.Models;
using WallPanelPlanner.ViewModels;

namespace WallPanelPlanner;

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
    private readonly List<HoleMappingEditor> holeMappingEditors = new();
    private bool isSyncingSelection;
    private bool showOnlyHoleMappingConflicts;
    private int visibleHoleMappingCount = HoleMappingBatchSize;

    public HardwareMappingPage()
    {
        InitializeComponent();
        viewModel = ((App)Application.Current!).GymSetupViewModel;
        RoomsPicker.ItemsSource = viewModel.Rooms;
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var selectedRoomName = viewModel.SelectedRoom?.Name;
            var selectedWallName = viewModel.SelectedWall?.Name;
            await viewModel.LoadWallsAsync();

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
            await DisplayAlertAsync("Mapping hardware", $"Errore inizializzazione mapping hardware: {ex.Message}", "OK");
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
        isSyncingSelection = false;

        var wall = viewModel.SelectedWall;
        WallInfoLabel.Text = wall is null
            ? "Nessuna parete selezionata."
            : $"{wall.DisplayLabel} - fori: {wall.GetOrderedHoles().Count}";
        SaveWallButton.IsEnabled = wall is not null;
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
        LoadMoreHoleMappingsButton.IsVisible = false;

        var wall = viewModel.SelectedWall;
        if (wall is null)
        {
            HoleMappingEmptyLabel.Text = "Seleziona una parete.";
            HoleMappingEmptyLabel.IsVisible = true;
            return;
        }

        var orderedHoles = wall.GetOrderedHoles();
        var conflictState = BuildHoleMappingConflictState(orderedHoles);
        if (orderedHoles.Count == 0)
        {
            HoleMappingEmptyLabel.Text = "Nessun foro disponibile.";
            HoleMappingEmptyLabel.IsVisible = true;
            return;
        }

        var holesToDisplay = showOnlyHoleMappingConflicts
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
        var pointIdEntry = new Entry
        {
            Text = hole.PointId,
            Placeholder = $"pointId foro {hole.Number}"
        };

        var ledIndexEntry = new Entry
        {
            Text = hole.LedIndex.ToString(CultureInfo.InvariantCulture),
            Keyboard = Keyboard.Numeric,
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
                SyncViewFromState();
            }
            catch (InvalidOperationException ex)
            {
                await DisplayAlertAsync("Mapping hardware", ex.Message, "OK");
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
        ledIndexEntry.Completed += async (_, _) => await ApplyAsync();
        ledIndexEntry.Unfocused += async (_, _) => await ApplyAsync();

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
            Text = "Attivo",
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
                        Text = $"Foro {hole.Number} - {hole.PanelName}",
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
                    hardwareGrid
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

    private void OnAutoRenumberHoleLedsClicked(object? sender, EventArgs e)
    {
        var wall = viewModel.SelectedWall;
        if (wall is null)
        {
            return;
        }

        foreach (var hole in wall.GetOrderedHoles().OrderBy(hole => hole.Number))
        {
            viewModel.UpdateHoleHardware(hole.Number, hole.PointId, hole.Number, hole.IsEnabled);
        }

        SyncViewFromState();
    }

    private void OnLoadMoreHoleMappingsClicked(object? sender, EventArgs e)
    {
        visibleHoleMappingCount += HoleMappingBatchSize;
        RebuildHoleMappingsList();
    }

    private async void OnSaveWallClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await viewModel.SaveSelectedWallAsync();
            await DisplayAlertAsync("Salvataggio completato", $"Parete salvata su database.\n{result}", "OK");
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Mapping hardware", ex.Message, "OK");
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
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
}
