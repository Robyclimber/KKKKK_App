using System.Globalization;
using RuoteLab.Models;

namespace RuoteLab;

public partial class SettingsPage : ContentPage
{
    private enum ColorTarget
    {
        RightHand,
        LeftHand,
        Start,
        Top
    }

    private App? app;
    private ColorTarget activeColorTarget = ColorTarget.RightHand;
    private bool isUpdatingColorControls;

    public SettingsPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSettings();
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
        EffectEntry.Text = defaults.Effect;
        DefaultBrightnessEntry.Text = defaults.DefaultBrightness.ToString(CultureInfo.InvariantCulture);
        DimmedBrightnessEntry.Text = defaults.DimmedBrightness.ToString(CultureInfo.InvariantCulture);
        RightHandColorValueLabel.Text = defaults.RightHandColor;
        LeftHandColorValueLabel.Text = defaults.LeftHandColor;
        StartColorValueLabel.Text = defaults.StartColor;
        TopColorValueLabel.Text = defaults.TopColor;
        BlinkCountEntry.Text = defaults.BlinkCount.ToString(CultureInfo.InvariantCulture);
        BlinkPeriodMsEntry.Text = defaults.BlinkPeriodMs.ToString(CultureInfo.InvariantCulture);
        HoldDurationMsEntry.Text = defaults.HoldDurationMs.ToString(CultureInfo.InvariantCulture);
        Esp32BaseUrlEntry.Text = esp32Settings.BaseUrl;
        Esp32ControllerIdEntry.Text = esp32Settings.ControllerId;
        Esp32WallLedCountEntry.Text = esp32Settings.WallLedCount.ToString(CultureInfo.InvariantCulture);
        Esp32BrightnessLimitEntry.Text = esp32Settings.BrightnessLimit.ToString(CultureInfo.InvariantCulture);

        RefreshColorPreviews();
        SetActiveColorTarget(activeColorTarget);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (app is null)
        {
            return;
        }

        try
        {
            var settings = BuildAppSettings();
            var esp32Settings = BuildEsp32Settings();
            app.AppSettingsService.Save(settings);
            app.Esp32SettingsService.Save(esp32Settings);
            StatusLabel.Text = "Impostazioni salvate. I nuovi circuiti useranno questi default.";
            await DisplayAlertAsync("Settings", "Impostazioni salvate correttamente.", "OK");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Errore salvataggio: {ex.Message}";
            await DisplayAlertAsync("Settings", ex.Message, "OK");
        }
    }

    private AppSettingsDefinition BuildAppSettings()
    {
        return new AppSettingsDefinition
        {
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
            }
        };
    }

    private Esp32DeviceSettings BuildEsp32Settings()
    {
        return new Esp32DeviceSettings
        {
            BaseUrl = ReadRequiredText(Esp32BaseUrlEntry.Text, "Inserisci un Base URL ESP32 valido."),
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

    private void RefreshColorPreviews()
    {
        ApplyColorPreview(RightHandColorPreview, RightHandColorValueLabel.Text);
        ApplyColorPreview(LeftHandColorPreview, LeftHandColorValueLabel.Text);
        ApplyColorPreview(StartColorPreview, StartColorValueLabel.Text);
        ApplyColorPreview(TopColorPreview, TopColorValueLabel.Text);
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

    private void UpdateColorPickerTexts(Color color)
    {
        RedValueLabel.Text = $"Rosso: {(int)Math.Round(color.Red * 255d)}";
        GreenValueLabel.Text = $"Verde: {(int)Math.Round(color.Green * 255d)}";
        BlueValueLabel.Text = $"Blu: {(int)Math.Round(color.Blue * 255d)}";
        ColorPickerPreview.Color = color;
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
