using System.Globalization;
using RouteLab.Models;

namespace RouteLab;

public partial class SettingsPage : ContentPage
{
    private enum ColorTarget
    {
        RightHand,
        LeftHand,
        Start,
        Top
    }

    private enum TrainingColorTarget
    {
        RestBlink,
        RestCompleted,
        ResistanceActive,
        ResistanceCompleted,
        HangActive,
        HangCompleted
    }

    private enum SettingsSection
    {
        Room,
        Climber,
        Circuit,
        Training,
        Controller
    }

    private App? app;
    private ColorTarget activeColorTarget = ColorTarget.RightHand;
    private TrainingColorTarget activeTrainingColorTarget = TrainingColorTarget.RestBlink;
    private SettingsSection? expandedSection = SettingsSection.Room;
    private bool isUpdatingColorControls;
    private bool isUpdatingTrainingColorControls;

    public SettingsPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
        RefreshSettingsSections();
    }

    private void OnRoomSectionToggleClicked(object? sender, EventArgs e)
    {
        ToggleSettingsSection(SettingsSection.Room);
    }

    private void OnCircuitSectionToggleClicked(object? sender, EventArgs e)
    {
        ToggleSettingsSection(SettingsSection.Circuit);
    }

    private void OnClimberSectionToggleClicked(object? sender, EventArgs e)
    {
        ToggleSettingsSection(SettingsSection.Climber);
    }

    private async void OnManageClimberProfilesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("biomechanical-profiles-page");
    }

    private void OnTrainingSectionToggleClicked(object? sender, EventArgs e)
    {
        ToggleSettingsSection(SettingsSection.Training);
    }

    private void OnControllerSectionToggleClicked(object? sender, EventArgs e)
    {
        ToggleSettingsSection(SettingsSection.Controller);
    }

    private void ToggleSettingsSection(SettingsSection section)
    {
        expandedSection = expandedSection == section ? null : section;
        RefreshSettingsSections();
    }

    private void RefreshSettingsSections()
    {
        SetSettingsSectionState(
            RoomSectionToggleButton,
            RoomSectionContent,
            "Sala",
            expandedSection == SettingsSection.Room);
        SetSettingsSectionState(
            ClimberSectionToggleButton,
            ClimberSectionContent,
            "Atleta",
            expandedSection == SettingsSection.Climber);
        SetSettingsSectionState(
            CircuitSectionToggleButton,
            CircuitSectionContent,
            "Circuiti",
            expandedSection == SettingsSection.Circuit);
        SetSettingsSectionState(
            TrainingSectionToggleButton,
            TrainingSectionContent,
            "Allenamento",
            expandedSection == SettingsSection.Training);
        SetSettingsSectionState(
            ControllerSectionToggleButton,
            ControllerSectionContent,
            "RouteLab Hub",
            expandedSection == SettingsSection.Controller);
    }

    private static void SetSettingsSectionState(
        Button toggleButton,
        VisualElement content,
        string title,
        bool isExpanded)
    {
        toggleButton.Text = $"{(isExpanded ? "-" : "+")}  {title}";
        content.IsVisible = isExpanded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (app is null)
        {
            return;
        }

        using var busy = AppBusy.Show("Caricamento impostazioni...");
        try
        {
            await app.GymSetupViewModel.EnsureLoadedAsync();
            LoadSettings();
            SyncRoomSettings();
        }
        catch (Exception ex)
        {
            RoomSettingsStatusLabel.Text = $"Errore caricamento sale: {ex.Message}";
        }
    }

    private void LoadSettings()
    {
        if (app is null)
        {
            return;
        }

        var settings = app.AppSettingsService.Load();
        var defaults = settings.CircuitDefaults;
        var esp32Settings = app.Esp32SettingsService.Load();

        PresetNameEntry.Text = defaults.PresetName;
        ClimberProfilesSummaryLabel.Text = settings.ClimberProfiles.Count == 1
            ? "1 profilo disponibile: Persona predefinita."
            : $"{settings.ClimberProfiles.Count} profili disponibili.";
        EffectEntry.Text = defaults.Effect;
        DefaultBrightnessEntry.Text = defaults.DefaultBrightness.ToString(CultureInfo.InvariantCulture);
        DimmedBrightnessEntry.Text = defaults.DimmedBrightness.ToString(CultureInfo.InvariantCulture);
        RightHandColorValueLabel.Text = defaults.RightHandColor;
        LeftHandColorValueLabel.Text = defaults.LeftHandColor;
        StartColorValueLabel.Text = defaults.StartColor;
        TopColorValueLabel.Text = defaults.TopColor;
        RestBlinkColorValueLabel.Text = settings.TrainingVisuals.RestBlinkColor;
        RestCompletedColorValueLabel.Text = settings.TrainingVisuals.RestCompletedColor;
        ResistanceActiveColorValueLabel.Text = settings.TrainingVisuals.ResistanceActiveColor;
        ResistanceCompletedColorValueLabel.Text = settings.TrainingVisuals.ResistanceCompletedColor;
        HangActiveColorValueLabel.Text = settings.TrainingVisuals.HangActiveColor;
        HangCompletedColorValueLabel.Text = settings.TrainingVisuals.HangCompletedColor;
        BlinkCountEntry.Text = defaults.BlinkCount.ToString(CultureInfo.InvariantCulture);
        BlinkPeriodMsEntry.Text = defaults.BlinkPeriodMs.ToString(CultureInfo.InvariantCulture);
        HoldDurationMsEntry.Text = defaults.HoldDurationMs.ToString(CultureInfo.InvariantCulture);
        Esp32BaseUrlEntry.Text = esp32Settings.BaseUrl;
        Esp32ControllerIdEntry.Text = esp32Settings.ControllerId;
        Esp32WallLedCountEntry.Text = esp32Settings.WallLedCount.ToString(CultureInfo.InvariantCulture);
        Esp32BrightnessLimitEntry.Text = esp32Settings.BrightnessLimit.ToString(CultureInfo.InvariantCulture);

        RefreshColorPreviews();
        RefreshTrainingColorPreviews();
        SetActiveColorTarget(activeColorTarget);
        SetActiveTrainingColorTarget(activeTrainingColorTarget);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (app is null)
        {
            return;
        }

        using var busy = AppBusy.Show("Salvataggio impostazioni...");
        try
        {
            var settings = BuildAppSettings();
            var esp32Settings = BuildEsp32Settings();
            app.AppSettingsService.Save(settings);
            app.Esp32SettingsService.Save(esp32Settings);
            StatusLabel.Text = "Impostazioni salvate. Circuiti e allenamento useranno i nuovi default.";
            await DisplayAlertAsync("Settings", "Impostazioni salvate correttamente.", "OK");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Errore salvataggio: {ex.Message}";
            await DisplayAlertAsync("Settings", ex.Message, "OK");
        }
    }

    private async void OnAddRoomClicked(object? sender, EventArgs e)
    {
        if (app is null)
        {
            return;
        }

        using var busy = AppBusy.Show("Creazione sala...");
        try
        {
            AddRoomButton.IsEnabled = false;
            await app.GymSetupViewModel.AddRoomAsync(NewRoomNameEntry.Text);
            var selectedRoom = app.GymSetupViewModel.SelectedRoom;
            NewRoomNameEntry.Text = string.Empty;
            SyncRoomSettings();
            RoomSettingsStatusLabel.Text = selectedRoom is null
                ? "Sala non creata."
                : $"Sala disponibile e selezionata: {selectedRoom.Name}";
        }
        catch (InvalidOperationException ex)
        {
            RoomSettingsStatusLabel.Text = ex.Message;
            await DisplayAlertAsync("Sala", ex.Message, "OK");
        }
        finally
        {
            AddRoomButton.IsEnabled = true;
        }
    }

    private void SyncRoomSettings()
    {
        if (app is null)
        {
            return;
        }

        NewRoomNameEntry.Placeholder = app.GymSetupViewModel.SuggestedNextRoomName;
    }

    private AppSettingsDefinition BuildAppSettings()
    {
        return new AppSettingsDefinition
        {
            ClimberProfiles = app?.AppSettingsService.Load().ClimberProfiles
                .Select(profile => profile.Clone())
                .ToList()
                ?? [new ClimberProfileDefinition()],
            CircuitDefaults = new CircuitGlobalsDefinition
            {
                PresetName = ReadRequiredText(PresetNameEntry.Text, "Inserisci un preset name valido."),
                Effect = ReadRequiredText(EffectEntry.Text, "Inserisci un effect valido."),
                DefaultBrightness = ParseRangeInt(DefaultBrightnessEntry.Text, 0, 255, "Default brightness deve essere tra 0 e 255."),
                DimmedBrightness = ParseRangeInt(DimmedBrightnessEntry.Text, 0, 255, "Dimmed brightness deve essere tra 0 e 255."),
                RightHandColor = ParseHexColor(RightHandColorValueLabel.Text, "Il colore mano destra deve essere in formato #RRGGBB."),
                LeftHandColor = ParseHexColor(LeftHandColorValueLabel.Text, "Il colore mano sinistra deve essere in formato #RRGGBB."),
                StartColor = ParseHexColor(StartColorValueLabel.Text, "Il colore start deve essere in formato #RRGGBB."),
                TopColor = ParseHexColor(TopColorValueLabel.Text, "Il colore top deve essere in formato #RRGGBB."),
                BlinkCount = ParseRangeInt(BlinkCountEntry.Text, 0, 20, "Blink count deve essere tra 0 e 20."),
                BlinkPeriodMs = ParseRangeInt(BlinkPeriodMsEntry.Text, 50, 5000, "Blink period deve essere tra 50 e 5000 ms."),
                HoldDurationMs = ParseRangeInt(HoldDurationMsEntry.Text, 100, 30000, "Hold duration deve essere tra 100 e 30000 ms.")
            },
            TrainingVisuals = new TrainingVisualSettingsDefinition
            {
                RestBlinkColor = ParseHexColor(RestBlinkColorValueLabel.Text, "Il colore recupero deve essere in formato #RRGGBB."),
                RestCompletedColor = ParseHexColor(RestCompletedColorValueLabel.Text, "Il colore fine recupero deve essere in formato #RRGGBB."),
                ResistanceActiveColor = ParseHexColor(ResistanceActiveColorValueLabel.Text, "Il colore resistenza deve essere in formato #RRGGBB."),
                ResistanceCompletedColor = ParseHexColor(ResistanceCompletedColorValueLabel.Text, "Il colore fine resistenza deve essere in formato #RRGGBB."),
                HangActiveColor = ParseHexColor(HangActiveColorValueLabel.Text, "Il colore sospensione deve essere in formato #RRGGBB."),
                HangCompletedColor = ParseHexColor(HangCompletedColorValueLabel.Text, "Il colore fine sospensione deve essere in formato #RRGGBB.")
            }
        };
    }

    private Esp32DeviceSettings BuildEsp32Settings()
    {
        return new Esp32DeviceSettings
        {
            BaseUrl = ReadRequiredText(Esp32BaseUrlEntry.Text, "Inserisci un Base URL valido per RouteLab Hub."),
            ControllerId = ReadRequiredText(Esp32ControllerIdEntry.Text, "Inserisci un Controller ID valido."),
            WallLedCount = ParsePositiveInt(Esp32WallLedCountEntry.Text, "LED parete deve essere un numero positivo."),
            BrightnessLimit = ParseRangeInt(Esp32BrightnessLimitEntry.Text, 0, 255, "Brightness limit deve essere tra 0 e 255.")
        };
    }

    private void OnPickRightHandColorClicked(object? sender, EventArgs e)
    {
        SetActiveColorTarget(ColorTarget.RightHand);
    }

    private void OnPickLeftHandColorClicked(object? sender, EventArgs e)
    {
        SetActiveColorTarget(ColorTarget.LeftHand);
    }

    private void OnPickStartColorClicked(object? sender, EventArgs e)
    {
        SetActiveColorTarget(ColorTarget.Start);
    }

    private void OnPickTopColorClicked(object? sender, EventArgs e)
    {
        SetActiveColorTarget(ColorTarget.Top);
    }

    private void OnPickRestBlinkColorClicked(object? sender, EventArgs e)
    {
        SetActiveTrainingColorTarget(TrainingColorTarget.RestBlink);
    }

    private void OnPickRestCompletedColorClicked(object? sender, EventArgs e)
    {
        SetActiveTrainingColorTarget(TrainingColorTarget.RestCompleted);
    }

    private void OnPickResistanceActiveColorClicked(object? sender, EventArgs e)
    {
        SetActiveTrainingColorTarget(TrainingColorTarget.ResistanceActive);
    }

    private void OnPickResistanceCompletedColorClicked(object? sender, EventArgs e)
    {
        SetActiveTrainingColorTarget(TrainingColorTarget.ResistanceCompleted);
    }

    private void OnPickHangActiveColorClicked(object? sender, EventArgs e)
    {
        SetActiveTrainingColorTarget(TrainingColorTarget.HangActive);
    }

    private void OnPickHangCompletedColorClicked(object? sender, EventArgs e)
    {
        SetActiveTrainingColorTarget(TrainingColorTarget.HangCompleted);
    }

    private void OnColorSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (isUpdatingColorControls)
        {
            return;
        }

        var color = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
        var hex = ToHexColor(color);
        ApplyColorToTarget(activeColorTarget, hex);
        UpdateColorPickerTexts(color);
    }

    private void OnTrainingColorSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (isUpdatingTrainingColorControls)
        {
            return;
        }

        var color = Color.FromRgb((byte)TrainingRedSlider.Value, (byte)TrainingGreenSlider.Value, (byte)TrainingBlueSlider.Value);
        var hex = ToHexColor(color);
        ApplyTrainingColorToTarget(activeTrainingColorTarget, hex);
        UpdateTrainingColorPickerTexts(color);
    }

    private void RefreshColorPreviews()
    {
        ApplyColorPreview(RightHandColorPreview, RightHandColorValueLabel.Text);
        ApplyColorPreview(LeftHandColorPreview, LeftHandColorValueLabel.Text);
        ApplyColorPreview(StartColorPreview, StartColorValueLabel.Text);
        ApplyColorPreview(TopColorPreview, TopColorValueLabel.Text);
    }

    private void RefreshTrainingColorPreviews()
    {
        ApplyColorPreview(RestBlinkColorPreview, RestBlinkColorValueLabel.Text);
        ApplyColorPreview(RestCompletedColorPreview, RestCompletedColorValueLabel.Text);
        ApplyColorPreview(ResistanceActiveColorPreview, ResistanceActiveColorValueLabel.Text);
        ApplyColorPreview(ResistanceCompletedColorPreview, ResistanceCompletedColorValueLabel.Text);
        ApplyColorPreview(HangActiveColorPreview, HangActiveColorValueLabel.Text);
        ApplyColorPreview(HangCompletedColorPreview, HangCompletedColorValueLabel.Text);
    }

    private void SetActiveColorTarget(ColorTarget target)
    {
        activeColorTarget = target;
        ColorPickerTargetLabel.Text = target switch
        {
            ColorTarget.RightHand => "Stai modificando: mano destra",
            ColorTarget.LeftHand => "Stai modificando: mano sinistra",
            ColorTarget.Start => "Stai modificando: start",
            ColorTarget.Top => "Stai modificando: top",
            _ => "Seleziona un colore da modificare."
        };

        var hex = GetColorValueForTarget(target);
        if (!TryParseColor(hex, out var color))
        {
            color = Color.FromArgb("#3A3120");
        }

        isUpdatingColorControls = true;
        RedSlider.Value = Math.Round(color.Red * 255d);
        GreenSlider.Value = Math.Round(color.Green * 255d);
        BlueSlider.Value = Math.Round(color.Blue * 255d);
        isUpdatingColorControls = false;
        UpdateColorPickerTexts(color);
    }

    private void SetActiveTrainingColorTarget(TrainingColorTarget target)
    {
        activeTrainingColorTarget = target;
        TrainingColorPickerTargetLabel.Text = target switch
        {
            TrainingColorTarget.RestBlink => "Stai modificando: recupero",
            TrainingColorTarget.RestCompleted => "Stai modificando: fine recupero",
            TrainingColorTarget.ResistanceActive => "Stai modificando: resistenza",
            TrainingColorTarget.ResistanceCompleted => "Stai modificando: fine resistenza",
            TrainingColorTarget.HangActive => "Stai modificando: sospensione",
            TrainingColorTarget.HangCompleted => "Stai modificando: fine sospensione",
            _ => "Seleziona un colore da modificare."
        };

        var hex = GetTrainingColorValueForTarget(target);
        if (!TryParseColor(hex, out var color))
        {
            color = Color.FromArgb("#3A3120");
        }

        isUpdatingTrainingColorControls = true;
        TrainingRedSlider.Value = Math.Round(color.Red * 255d);
        TrainingGreenSlider.Value = Math.Round(color.Green * 255d);
        TrainingBlueSlider.Value = Math.Round(color.Blue * 255d);
        isUpdatingTrainingColorControls = false;
        UpdateTrainingColorPickerTexts(color);
    }

    private string GetColorValueForTarget(ColorTarget target)
    {
        return target switch
        {
            ColorTarget.RightHand => RightHandColorValueLabel.Text ?? "#C44536",
            ColorTarget.LeftHand => LeftHandColorValueLabel.Text ?? "#247BA0",
            ColorTarget.Start => StartColorValueLabel.Text ?? "#FFFF00",
            ColorTarget.Top => TopColorValueLabel.Text ?? "#FF0000",
            _ => "#3A3120"
        };
    }

    private string GetTrainingColorValueForTarget(TrainingColorTarget target)
    {
        return target switch
        {
            TrainingColorTarget.RestBlink => RestBlinkColorValueLabel.Text ?? "#FF0000",
            TrainingColorTarget.RestCompleted => RestCompletedColorValueLabel.Text ?? "#00FF00",
            TrainingColorTarget.ResistanceActive => ResistanceActiveColorValueLabel.Text ?? "#FF8C00",
            TrainingColorTarget.ResistanceCompleted => ResistanceCompletedColorValueLabel.Text ?? "#00FF00",
            TrainingColorTarget.HangActive => HangActiveColorValueLabel.Text ?? "#00BFFF",
            TrainingColorTarget.HangCompleted => HangCompletedColorValueLabel.Text ?? "#00FF00",
            _ => "#3A3120"
        };
    }

    private void ApplyColorToTarget(ColorTarget target, string hex)
    {
        switch (target)
        {
            case ColorTarget.RightHand:
                RightHandColorValueLabel.Text = hex;
                break;
            case ColorTarget.LeftHand:
                LeftHandColorValueLabel.Text = hex;
                break;
            case ColorTarget.Start:
                StartColorValueLabel.Text = hex;
                break;
            case ColorTarget.Top:
                TopColorValueLabel.Text = hex;
                break;
        }

        RefreshColorPreviews();
    }

    private void ApplyTrainingColorToTarget(TrainingColorTarget target, string hex)
    {
        switch (target)
        {
            case TrainingColorTarget.RestBlink:
                RestBlinkColorValueLabel.Text = hex;
                break;
            case TrainingColorTarget.RestCompleted:
                RestCompletedColorValueLabel.Text = hex;
                break;
            case TrainingColorTarget.ResistanceActive:
                ResistanceActiveColorValueLabel.Text = hex;
                break;
            case TrainingColorTarget.ResistanceCompleted:
                ResistanceCompletedColorValueLabel.Text = hex;
                break;
            case TrainingColorTarget.HangActive:
                HangActiveColorValueLabel.Text = hex;
                break;
            case TrainingColorTarget.HangCompleted:
                HangCompletedColorValueLabel.Text = hex;
                break;
        }

        RefreshTrainingColorPreviews();
    }

    private void UpdateColorPickerTexts(Color color)
    {
        RedValueLabel.Text = $"Rosso: {(int)Math.Round(color.Red * 255d)}";
        GreenValueLabel.Text = $"Verde: {(int)Math.Round(color.Green * 255d)}";
        BlueValueLabel.Text = $"Blu: {(int)Math.Round(color.Blue * 255d)}";
        ColorPickerPreview.Color = color;
    }

    private void UpdateTrainingColorPickerTexts(Color color)
    {
        TrainingRedValueLabel.Text = $"Rosso: {(int)Math.Round(color.Red * 255d)}";
        TrainingGreenValueLabel.Text = $"Verde: {(int)Math.Round(color.Green * 255d)}";
        TrainingBlueValueLabel.Text = $"Blu: {(int)Math.Round(color.Blue * 255d)}";
        TrainingColorPickerPreview.Color = color;
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

    private static int ParsePositiveInt(string? text, string errorMessage)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value > 0)
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static double ParseRangeDouble(string? text, double min, double max, string errorMessage)
    {
        var normalized = text?.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
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



