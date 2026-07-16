using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using RuoteLab.Drawing;
using RuoteLab.Models;
using RuoteLab.Services;

namespace RuoteLab;

public partial class CircuitRunnerPage : ContentPage
{
    private readonly App app;
    private readonly CircuitEditorDrawable previewDrawable = new();
    private IReadOnlyList<WallDefinition> availableWalls = Array.Empty<WallDefinition>();
    private IReadOnlyList<CircuitDefinition> availableCircuits = Array.Empty<CircuitDefinition>();
    private string? selectedRoomName;
    private WallDefinition? selectedWall;
    private CircuitDefinition? selectedCircuit;
    private bool isRefreshing;
    private bool isBusyWithEsp32;
    private double previewZoom = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;

    public CircuitRunnerPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
        CircuitPreviewCanvas.Drawable = previewDrawable;
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
            await app.GymSetupViewModel.EnsureLoadedAsync();
            await app.CircuitEditorViewModel.LoadCircuitsAsync();
            availableWalls = app.GymSetupViewModel.Walls.OrderBy(wall => wall.RoomName).ThenBy(wall => wall.Name).ToList();
            availableCircuits = app.CircuitEditorViewModel.Circuits.ToList();
            selectedRoomName ??= app.GymSetupViewModel.AvailableRoomNames.FirstOrDefault();
            selectedWall ??= GetVisibleWalls().FirstOrDefault();
            if (selectedWall is not null && !string.Equals(selectedRoomName, selectedWall.RoomName, StringComparison.Ordinal))
            {
                selectedRoomName = selectedWall.RoomName;
            }

            SyncSelectedCircuit();
            RefreshView();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Circuiti", $"Errore caricamento pagina esecuzione: {ex.Message}", "OK");
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void OnRoomChanged(object? sender, EventArgs e)
    {
        selectedRoomName = RoomPicker.SelectedItem as string;
        selectedWall = GetVisibleWalls().FirstOrDefault();
        SyncSelectedCircuit();
        RefreshView();
    }

    private void OnWallChanged(object? sender, EventArgs e)
    {
        selectedWall = WallPicker.SelectedItem as WallDefinition;
        if (selectedWall is not null)
        {
            selectedRoomName = selectedWall.RoomName;
        }

        SyncSelectedCircuit();
        RefreshView();
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom + 0.25d, 1d, 4d);
        UpdatePreviewZoomLayout();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom - 0.25d, 1d, 4d);
        UpdatePreviewZoomLayout();
    }

    private void OnZoomResetClicked(object? sender, EventArgs e)
    {
        previewZoom = 1d;
        UpdatePreviewZoomLayout();
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

    private async void OnVisualizeCircuitClicked(object? sender, EventArgs e)
    {
        await RunCircuitCommandAsync(
            "Visualizzazione circuito in corso...",
            async settings =>
            {
                var circuitId = await EnsureSelectedCircuitSyncedAsync(settings);
                var response = await app.Esp32ApiClient.VisualizeCircuitAsync(settings, circuitId);
                return response.Success
                    ? $"Visualizza OK - {selectedCircuit!.Name}"
                    : $"Visualizza KO - {response.ErrorCode} - {response.Message}";
            });
    }

    private async void OnStartCircuitClicked(object? sender, EventArgs e)
    {
        await RunCircuitCommandAsync(
            "Avvio circuito in corso...",
            async settings =>
            {
                var circuitId = await EnsureSelectedCircuitSyncedAsync(settings);
                var response = await app.Esp32ApiClient.StartCircuitAsync(settings, circuitId);
                return response.Success
                    ? $"Avvio OK - {selectedCircuit!.Name}"
                    : $"Avvio KO - {response.ErrorCode} - {response.Message}";
            });
    }

    private async void OnStopCircuitClicked(object? sender, EventArgs e)
    {
        await RunCircuitCommandAsync(
            "Stop circuito in corso...",
            async settings =>
            {
                var response = await app.Esp32ApiClient.StopCircuitAsync(settings);
                return response.Success
                    ? "Stop / Spegni OK."
                    : $"Stop KO - {response.ErrorCode} - {response.Message}";
            });
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

        SyncSelectedCircuit();
        previewDrawable.Wall = selectedWall;
        previewDrawable.Circuit = selectedCircuit;
        previewDrawable.HighlightedHole = null;

        RunnerSummaryLabel.Text = BuildSummaryText();
        SelectedCircuitLabel.Text = selectedCircuit is null
            ? "Nessun circuito selezionato."
            : $"Circuito selezionato: {selectedCircuit.Name} - Diff {selectedCircuit.Difficulty} - Incl. {selectedCircuit.Inclination}";

        RebuildCircuitsList();
        CircuitsEmptyLabel.IsVisible = GetVisibleCircuits().Count == 0;
        VisualizeCircuitButton.IsEnabled = selectedCircuit is not null && !isBusyWithEsp32;
        StartCircuitButton.IsEnabled = selectedCircuit is not null && !isBusyWithEsp32;
        StopCircuitButton.IsEnabled = !isBusyWithEsp32;
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
    }

    private string BuildSummaryText()
    {
        if (selectedWall is null)
        {
            return "Seleziona sala e parete per usare i circuiti.";
        }

        var circuitCount = GetVisibleCircuits().Count;
        return $"Sala {selectedWall.RoomName} - Parete {selectedWall.Name} - Circuiti disponibili: {circuitCount}";
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
                string.Equals(circuit.WallName, selectedWall.Name, StringComparison.Ordinal))
            .OrderBy(circuit => circuit.Name)
            .ToList();
    }

    private void SyncSelectedCircuit()
    {
        var visibleCircuits = GetVisibleCircuits();
        if (selectedCircuit is null || !visibleCircuits.Any(circuit => circuit.Id == selectedCircuit.Id))
        {
            selectedCircuit = visibleCircuits.FirstOrDefault();
        }
    }

    private void RebuildCircuitsList()
    {
        CircuitsHost.Children.Clear();

        foreach (var circuit in GetVisibleCircuits())
        {
            var isSelected = selectedCircuit?.Id == circuit.Id;
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
                        Text = $"Movimenti: {circuit.Movements.Count}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#D8A72D")
                    }
                }
            };

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    selectedCircuit = circuit;
                    RefreshView();
                })
            });

            CircuitsHost.Children.Add(border);
        }
    }

    private void UpdatePreviewBaseScale()
    {
        if (selectedWall is null || selectedWall.Width <= 0 || selectedWall.Height <= 0)
        {
            previewDrawable.PixelsPerMillimeter = 0.1f;
            return;
        }

        const double padding = 48d;
        var availableWidth = Math.Max(1d, basePreviewWidth - padding);
        var availableHeight = Math.Max(1d, basePreviewHeight - padding);
        var fitScale = Math.Min(availableWidth / selectedWall.Width, availableHeight / selectedWall.Height);
        previewDrawable.PixelsPerMillimeter = (float)Math.Max(0.01d, fitScale);
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

    private void UpdateWallImageOverlay()
    {
        CircuitPanelImagesHost.Children.Clear();
        if (selectedWall is null)
        {
            return;
        }

        var wallBounds = previewDrawable.GetWallBounds();
        var scale = Math.Max(0.01f, previewDrawable.PixelsPerMillimeter * previewDrawable.ZoomFactor);

        foreach (var panel in selectedWall.Panels.Where(panel => !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)))
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

    private async Task RunCircuitCommandAsync(string busyMessage, Func<Esp32DeviceSettings, Task<string>> action)
    {
        if (isBusyWithEsp32)
        {
            return;
        }

        try
        {
            isBusyWithEsp32 = true;
            RefreshView();
            Esp32StatusLabel.Text = busyMessage;
            var settings = app.Esp32SettingsService.Load();
            app.Esp32SettingsService.Save(settings);
            Esp32StatusLabel.Text = await action(settings);
        }
        catch (Exception ex)
        {
            Esp32StatusLabel.Text = $"Errore ESP32: {ex.Message}";
        }
        finally
        {
            isBusyWithEsp32 = false;
            RefreshView();
        }
    }

    private async Task<string> EnsureSelectedCircuitSyncedAsync(Esp32DeviceSettings settings)
    {
        if (selectedWall is null)
        {
            throw new InvalidOperationException("Seleziona prima una parete.");
        }

        if (selectedCircuit is null)
        {
            throw new InvalidOperationException("Seleziona prima un circuito.");
        }

        var room = app.GymSetupViewModel.Rooms.FirstOrDefault(item => string.Equals(item.Name, selectedWall.RoomName, StringComparison.Ordinal))
                   ?? throw new InvalidOperationException("La sala della parete selezionata non e' disponibile.");
        var wallId = Esp32PayloadBuilderService.BuildWallId(selectedWall);
        var localCircuits = GetVisibleCircuits();
        if (localCircuits.Count == 0)
        {
            throw new InvalidOperationException("La parete selezionata non contiene circuiti.");
        }

        var statusResponse = await app.Esp32ApiClient.GetStatusAsync(settings);
        if (!statusResponse.Success)
        {
            throw new InvalidOperationException($"Status ESP32 non disponibile: {statusResponse.ErrorCode} - {statusResponse.Message}");
        }

        var requiresCircuitSync = !string.Equals(statusResponse.Data?.ConfiguredWallId, wallId, StringComparison.Ordinal);
        if (requiresCircuitSync)
        {
            var wallPayload = app.Esp32PayloadBuilderService.BuildWallConfig(selectedWall, room, settings);
            var configResponse = await app.Esp32ApiClient.PostConfigAsync(settings, wallPayload);
            if (!configResponse.Success)
            {
                throw new InvalidOperationException($"Invio config parete fallito: {configResponse.ErrorCode} - {configResponse.Message}");
            }
        }

        if (!requiresCircuitSync)
        {
            var remoteCircuitsResponse = await app.Esp32ApiClient.GetCircuitsAsync(settings);
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
            var circuitsPayload = app.Esp32PayloadBuilderService.BuildCircuitsPayload(selectedWall, room, localCircuits);
            var circuitsResponse = await app.Esp32ApiClient.PostCircuitsAsync(settings, circuitsPayload);
            if (!circuitsResponse.Success)
            {
                throw new InvalidOperationException($"Sync circuiti fallita: {circuitsResponse.ErrorCode} - {circuitsResponse.Message}");
            }
        }

        return Esp32PayloadBuilderService.BuildCircuitId(selectedCircuit);
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
}
