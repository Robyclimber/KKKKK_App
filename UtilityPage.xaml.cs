using System.Globalization;
using WallPanelPlanner.Models;
using WallPanelPlanner.Services;

namespace WallPanelPlanner;

public partial class UtilityPage : ContentPage
{
    private App? app;
    private bool isResetting;
    private bool isBusyWithEsp32;
    private IReadOnlyList<WallDefinition> availableWalls = Array.Empty<WallDefinition>();
    private IReadOnlyList<CircuitDefinition> availableCircuits = Array.Empty<CircuitDefinition>();

    public UtilityPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadEsp32StateAsync();
    }

    private async void OnResetDatabaseClicked(object? sender, EventArgs e)
    {
        if (isResetting)
        {
            return;
        }

        var confirm = await DisplayAlertAsync(
            "Conferma reset",
            "Questo resetta completamente il database dell'app. Vuoi continuare?",
            "Si, resetta",
            "Annulla");

        if (!confirm)
        {
            return;
        }

        try
        {
            isResetting = true;
            ResetStatusLabel.Text = "Reset in corso...";

            var app = (App)Application.Current!;
            await app.SqliteDatabaseFactory.ResetAllDataAsync();
            await app.GymSetupViewModel.LoadWallsAsync();
            await app.CircuitEditorViewModel.LoadCircuitsAsync();

            ResetStatusLabel.Text = "Database resettato correttamente.";
            await DisplayAlertAsync("Utility", "Database resettato. Puoi ricreare tutto da zero.", "OK");
        }
        catch (Exception ex)
        {
            ResetStatusLabel.Text = "Errore durante il reset.";
            await DisplayAlertAsync("Utility", $"Errore reset database: {ex.Message}", "OK");
        }
        finally
        {
            isResetting = false;
        }
    }

    private async Task LoadEsp32StateAsync()
    {
        if (app is null)
        {
            return;
        }

        var settings = app.Esp32SettingsService.Load();
        Esp32BaseUrlEntry.Text = settings.BaseUrl;
        Esp32ControllerIdEntry.Text = settings.ControllerId;
        Esp32WallLedCountEntry.Text = settings.WallLedCount.ToString(CultureInfo.InvariantCulture);
        Esp32BrightnessLimitEntry.Text = settings.BrightnessLimit.ToString(CultureInfo.InvariantCulture);

        availableWalls = await app.WallRepository.GetAllAsync();
        availableCircuits = await app.CircuitRepository.GetAllAsync();
        Esp32WallPicker.ItemsSource = availableWalls.ToList();

        if (availableWalls.Count > 0)
        {
            Esp32WallPicker.SelectedItem ??= availableWalls.FirstOrDefault();
        }

        RefreshCircuitPicker();
    }

    private void OnEsp32WallSelectionChanged(object? sender, EventArgs e)
    {
        RefreshCircuitPicker();
    }

    private void RefreshCircuitPicker()
    {
        var selectedWall = Esp32WallPicker.SelectedItem as WallDefinition;
        IReadOnlyList<CircuitDefinition> visibleCircuits = selectedWall is null
            ? Array.Empty<CircuitDefinition>()
            : availableCircuits
                .Where(circuit =>
                    string.Equals(circuit.RoomName, selectedWall.RoomName, StringComparison.Ordinal) &&
                    string.Equals(circuit.WallName, selectedWall.Name, StringComparison.Ordinal))
                .OrderBy(circuit => circuit.Name)
                .ToList();

        Esp32CircuitPicker.ItemsSource = visibleCircuits.ToList();
        Esp32CircuitPicker.SelectedItem = visibleCircuits.FirstOrDefault();
    }

    private void OnSaveEsp32SettingsClicked(object? sender, EventArgs e)
    {
        var settings = ReadEsp32Settings();
        app?.Esp32SettingsService.Save(settings);
        Esp32StatusLabel.Text = "Impostazioni ESP32 salvate.";
    }

    private async void OnEsp32HealthClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.GetHealthAsync(settings);
            return response.Success
                ? $"Health OK - status {response.Data?.Status} - FW {response.Data?.FirmwareVersion}"
                : $"Health KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnEsp32StatusClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.GetStatusAsync(settings);
            return response.Success
                ? $"Status OK - state {response.Data?.RuntimeState} - wifi {response.Data?.WifiStatus} - circuiti {response.Data?.CircuitsCount} - attivo {response.Data?.ActiveCircuitId ?? "nessuno"}"
                : $"Status KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnSendWallConfigClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var (wall, room) = GetSelectedWallAndRoom();
            var payload = app!.Esp32PayloadBuilderService.BuildWallConfig(wall, room, settings);
            var response = await app.Esp32ApiClient.PostConfigAsync(settings, payload);
            return response.Success
                ? $"Config parete inviata - wallId {payload.WallId} - punti {payload.Points.Count}"
                : $"Config KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnSyncCircuitsClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var (wall, room) = GetSelectedWallAndRoom();
            var payload = app!.Esp32PayloadBuilderService.BuildCircuitsPayload(wall, room, availableCircuits);
            var response = await app.Esp32ApiClient.PostCircuitsAsync(settings, payload);
            return response.Success
                ? $"Circuiti sincronizzati - wallId {payload.WallId} - circuiti {payload.Circuits.Count}"
                : $"Sync KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnShowCircuitClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var circuit = Esp32CircuitPicker.SelectedItem as CircuitDefinition
                          ?? throw new InvalidOperationException("Seleziona prima un circuito.");
            var circuitId = Esp32PayloadBuilderService.BuildCircuitId(circuit);
            var response = await app!.Esp32ApiClient.ShowCircuitAsync(settings, circuitId);
            return response.Success
                ? $"Show circuito OK - {circuitId}"
                : $"Show KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnStopCircuitClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.StopCircuitAsync(settings);
            return response.Success
                ? "Stop circuito OK."
                : $"Stop KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnResetCircuitClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.ResetCircuitAsync(settings);
            return response.Success
                ? "Reset circuito OK."
                : $"Reset KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnClearCircuitClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.ClearCircuitAsync(settings);
            return response.Success
                ? "Clear LED OK."
                : $"Clear KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnRandomSequenceTestClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.StartRandomSequenceTestAsync(settings);
            return response.Success
                ? "Test random LED avviato."
                : $"Test random KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnStopRandomSequenceTestClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.StopCircuitAsync(settings);
            return response.Success
                ? "Test random LED fermato."
                : $"Stop test random KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async Task RunEsp32ActionAsync(Func<Esp32DeviceSettings, Task<string>> action)
    {
        if (isBusyWithEsp32)
        {
            return;
        }

        try
        {
            isBusyWithEsp32 = true;
            Esp32StatusLabel.Text = "Chiamata ESP32 in corso...";
            var settings = ReadEsp32Settings();
            app?.Esp32SettingsService.Save(settings);
            Esp32StatusLabel.Text = await action(settings);
        }
        catch (Exception ex)
        {
            Esp32StatusLabel.Text = $"Errore ESP32: {ex.Message}";
        }
        finally
        {
            isBusyWithEsp32 = false;
        }
    }

    private (WallDefinition Wall, RoomDefinition Room) GetSelectedWallAndRoom()
    {
        var wall = Esp32WallPicker.SelectedItem as WallDefinition
                   ?? throw new InvalidOperationException("Seleziona prima una parete.");
        var room = app!.GymSetupViewModel.Rooms.FirstOrDefault(item => string.Equals(item.Name, wall.RoomName, StringComparison.Ordinal))
                   ?? throw new InvalidOperationException("La sala della parete selezionata non e' disponibile.");
        return (wall, room);
    }

    private Esp32DeviceSettings ReadEsp32Settings()
    {
        return new Esp32DeviceSettings
        {
            BaseUrl = Esp32BaseUrlEntry.Text?.Trim() ?? string.Empty,
            ControllerId = string.IsNullOrWhiteSpace(Esp32ControllerIdEntry.Text) ? "esp32-sala-1" : Esp32ControllerIdEntry.Text.Trim(),
            WallLedCount = ParsePositiveInt(Esp32WallLedCountEntry.Text, "Inserisci un numero LED valido."),
            BrightnessLimit = ParseRangeInt(Esp32BrightnessLimitEntry.Text, 0, 255, "Inserisci un brightness limit tra 0 e 255.")
        };
    }

    private static int ParsePositiveInt(string? text, string errorMessage)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static int ParseRangeInt(string? text, int min, int max, string errorMessage)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }
}
