using System.Globalization;
using RouteLab.Models;
using RouteLab.Services;

namespace RouteLab;

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
        using var busy = AppBusy.Show("Connessione a RouteLab Hub...");
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

        await app.GymSetupViewModel.EnsureLoadedAsync();

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
                    circuit.GetWallNames().Count == 1 &&
                    circuit.UsesWall(selectedWall.Name))
                .OrderBy(circuit => circuit.Name)
                .ToList();

        Esp32CircuitPicker.ItemsSource = visibleCircuits.ToList();
        Esp32CircuitPicker.SelectedItem = visibleCircuits.FirstOrDefault();
    }

    private async void OnSaveEsp32SettingsClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Salvataggio impostazioni...");
        await Task.Yield();
        var settings = ReadEsp32Settings();
        app?.Esp32SettingsService.Save(settings);
        Esp32StatusLabel.Text = "Impostazioni RouteLab Hub salvate.";
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

    private async void OnSendSignTextClicked(object? sender, EventArgs e)
    {
        var text = SignTextEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            Esp32StatusLabel.Text = "Scrivi prima una frase da mostrare sulla matrice.";
            return;
        }

        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.SetSignTextAsync(settings, text);
            return response.Success
                ? $"Scritta matrice aggiornata: {text}"
                : $"Scritta matrice KO - {response.ErrorCode} - {response.Message}";
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
        var (wall, room) = GetSelectedWallAndRoom();
        var circuitsForWall = availableCircuits
            .Where(circuit =>
                string.Equals(circuit.RoomName, room.Name, StringComparison.Ordinal) &&
                circuit.GetWallNames().Count == 1 &&
                circuit.UsesWall(wall.Name))
            .ToList();

        var confirm = await DisplayAlertAsync(
            "Conferma sync circuiti",
            $"Stai per sovrascrivere i circuiti gia' presenti sul dispositivo per la parete \"{wall.Name}\".\n\nCircuiti inviati: {circuitsForWall.Count}\n\nVuoi continuare?",
            "Si, sovrascrivi",
            "Annulla");

        if (!confirm)
        {
            Esp32StatusLabel.Text = "Sync circuiti annullata.";
            return;
        }

        await RunEsp32ActionAsync(async settings =>
        {
            var payload = app!.Esp32PayloadBuilderService.BuildCircuitsPayload(wall, room, availableCircuits);
            var response = await app.Esp32ApiClient.PostCircuitsAsync(settings, payload);
            return response.Success
                ? $"Circuiti sincronizzati - wallId {payload.WallId} - circuiti {payload.Circuits.Count}"
                : $"Sync KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnSyncEditorialCircuitsClicked(object? sender, EventArgs e)
    {
        var (wall, room) = GetSelectedWallAndRoom();
        var circuitsForWall = availableCircuits
            .Where(circuit =>
                string.Equals(circuit.RoomName, room.Name, StringComparison.Ordinal) &&
                circuit.GetWallNames().Count == 1 &&
                circuit.UsesWall(wall.Name))
            .ToList();

        await RunEsp32ActionAsync(async settings =>
        {
            var payload = BuildEditorialCircuitsPayload(wall, circuitsForWall);
            var response = await app!.Esp32ApiClient.PostEditorialCircuitsAsync(settings, payload);
            return response.Success
                ? $"Circuiti editoriali sincronizzati - wallId {payload.WallId} - circuiti {payload.Circuits.Count}"
                : $"Sync editoriale KO - {response.ErrorCode} - {response.Message}";
        });
    }

    private async void OnImportEditorialCircuitsClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.GetEditorialCircuitsAsync(settings);
            if (!response.Success)
            {
                return $"Import KO - {response.ErrorCode} - {response.Message}";
            }

            var importedCount = await ImportEditorialCircuitsAsync(response.Data);
            await app.CircuitEditorViewModel.LoadCircuitsAsync();
            availableCircuits = await app.CircuitRepository.GetAllAsync();
            RefreshCircuitPicker();
            return $"Import OK - circuiti importati o aggiornati: {importedCount}";
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

    private async void OnAllLedsTestClicked(object? sender, EventArgs e)
    {
        await RunEsp32ActionAsync(async settings =>
        {
            var response = await app!.Esp32ApiClient.StartAllLedsTestAsync(settings);
            return response.Success
                ? $"Test LED OK - accesi {settings.WallLedCount} LED a luminosità {settings.BrightnessLimit}. Usa Clear LED per spegnerli."
                : $"Test tutti LED KO - {response.ErrorCode} - {response.Message}";
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

        using var busy = AppBusy.Show("Comunicazione con RouteLab Hub...");
        try
        {
            isBusyWithEsp32 = true;
            Esp32StatusLabel.Text = "Chiamata a RouteLab Hub in corso...";
            var settings = ReadEsp32Settings();
            app?.Esp32SettingsService.Save(settings);
            Esp32StatusLabel.Text = await action(settings);
        }
        catch (Exception ex)
        {
            Esp32StatusLabel.Text = $"Errore RouteLab Hub: {ex.Message}";
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
            ControllerId = string.IsNullOrWhiteSpace(Esp32ControllerIdEntry.Text) ? "routelab-hub-sala-1" : Esp32ControllerIdEntry.Text.Trim(),
            WallLedCount = ParsePositiveInt(Esp32WallLedCountEntry.Text, "Inserisci un numero LED valido."),
            BrightnessLimit = ParseRangeInt(Esp32BrightnessLimitEntry.Text, 0, 255, "Inserisci un brightness limit tra 0 e 255.")
        };
    }

    private Esp32EditorialCircuitsPayload BuildEditorialCircuitsPayload(WallDefinition wall, IReadOnlyList<CircuitDefinition> circuits)
    {
        var wallId = Esp32PayloadBuilderService.BuildWallId(wall);
        return new Esp32EditorialCircuitsPayload
        {
            WallId = wallId,
            ReplaceAll = true,
            Circuits = circuits.Select(circuit => new Esp32EditorialCircuitPayload
            {
                CircuitId = Esp32PayloadBuilderService.BuildCircuitId(circuit),
                Name = circuit.Name,
                WallId = wallId,
                Difficulty = circuit.Difficulty,
                Inclination = circuit.Inclination,
                Globals = new Esp32EditorialCircuitGlobalsPayload
                {
                    PresetName = circuit.Globals.PresetName,
                    Effect = circuit.Globals.Effect,
                    DefaultBrightness = circuit.Globals.DefaultBrightness,
                    DimmedBrightness = circuit.Globals.DimmedBrightness,
                    RightHandColor = circuit.Globals.RightHandColor,
                    LeftHandColor = circuit.Globals.LeftHandColor,
                    StartColor = circuit.Globals.StartColor,
                    TopColor = circuit.Globals.TopColor,
                    BlinkCount = circuit.Globals.BlinkCount,
                    BlinkPeriodMs = circuit.Globals.BlinkPeriodMs,
                    HoldDurationMs = circuit.Globals.HoldDurationMs
                },
                Movements = circuit.Movements
                    .OrderBy(movement => movement.Sequence)
                    .Select(movement => new Esp32EditorialCircuitMovementPayload
                    {
                        P = movement.HoleNumber,
                        H = movement.Hand == HandSide.Left ? 0 : 1,
                        R = movement.Role switch
                        {
                            MovementRole.Start => 1,
                            MovementRole.Top => 2,
                            MovementRole.Feet => 3,
                            _ => 0
                        },
                        S = movement.Sequence
                    })
                    .ToList()
            }).ToList()
        };
    }

    private async Task<int> ImportEditorialCircuitsAsync(Esp32EditorialCircuitsCatalogData? data)
    {
        if (data is null || string.IsNullOrWhiteSpace(data.WallId))
        {
            throw new InvalidOperationException("Catalogo circuiti editoriali non valido.");
        }

        var wall = app!.GymSetupViewModel.Walls.FirstOrDefault(candidate =>
            string.Equals(Esp32PayloadBuilderService.BuildWallId(candidate), data.WallId, StringComparison.Ordinal));
        if (wall is null)
        {
            throw new InvalidOperationException($"Nessuna parete locale corrisponde a wallId {data.WallId}.");
        }

        var existingCircuits = (await app.CircuitRepository.GetAllAsync()).ToList();
        var importedCount = 0;

        foreach (var remoteCircuit in data.Circuits)
        {
            if (string.IsNullOrWhiteSpace(remoteCircuit.CircuitId))
            {
                continue;
            }

            var localCircuit = existingCircuits.FirstOrDefault(circuit =>
                string.Equals(Esp32PayloadBuilderService.BuildCircuitId(circuit), remoteCircuit.CircuitId, StringComparison.Ordinal));

            localCircuit ??= new CircuitDefinition
            {
                CircuitId = remoteCircuit.CircuitId,
                RoomName = wall.RoomName,
                WallName = wall.Name,
                Name = remoteCircuit.Name
            };

            localCircuit.CircuitId = remoteCircuit.CircuitId;
            localCircuit.RoomName = wall.RoomName;
            localCircuit.SetWallNames(new[] { wall.Name });
            localCircuit.Name = remoteCircuit.Name;
            localCircuit.Difficulty = remoteCircuit.Difficulty;
            localCircuit.Inclination = remoteCircuit.Inclination;
            localCircuit.Globals = new CircuitGlobalsDefinition
            {
                PresetName = remoteCircuit.Globals.PresetName,
                Effect = remoteCircuit.Globals.Effect,
                DefaultBrightness = remoteCircuit.Globals.DefaultBrightness,
                DimmedBrightness = remoteCircuit.Globals.DimmedBrightness,
                RightHandColor = remoteCircuit.Globals.RightHandColor,
                LeftHandColor = remoteCircuit.Globals.LeftHandColor,
                StartColor = remoteCircuit.Globals.StartColor,
                TopColor = remoteCircuit.Globals.TopColor,
                BlinkCount = remoteCircuit.Globals.BlinkCount,
                BlinkPeriodMs = remoteCircuit.Globals.BlinkPeriodMs,
                HoldDurationMs = remoteCircuit.Globals.HoldDurationMs
            };

            localCircuit.Movements.Clear();
            foreach (var movement in remoteCircuit.Movements.OrderBy(movement => movement.S))
            {
                localCircuit.Movements.Add(new CircuitMovementDefinition
                {
                    WallName = wall.Name,
                    HoleNumber = movement.P,
                    Hand = movement.H == 0 ? HandSide.Left : HandSide.Right,
                    Role = movement.R switch
                    {
                        1 => MovementRole.Start,
                        2 => MovementRole.Top,
                        3 => MovementRole.Feet,
                        _ => MovementRole.Normal
                    },
                    Sequence = movement.S
                });
            }

            await app.CircuitRepository.SaveAsync(localCircuit);
            if (localCircuit.Id > 0 && !existingCircuits.Any(circuit => circuit.Id == localCircuit.Id))
            {
                existingCircuits.Add(localCircuit);
            }

            importedCount++;
        }

        return importedCount;
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
