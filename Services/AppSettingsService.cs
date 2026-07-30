using Microsoft.Maui.Storage;
using RuoteLab.Models;

namespace RuoteLab.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private const string PresetNameKey = "app.circuitDefaults.presetName";
    private const string EffectKey = "app.circuitDefaults.effect";
    private const string DefaultBrightnessKey = "app.circuitDefaults.defaultBrightness";
    private const string DimmedBrightnessKey = "app.circuitDefaults.dimmedBrightness";
    private const string RightHandColorKey = "app.circuitDefaults.rightHandColor";
    private const string LeftHandColorKey = "app.circuitDefaults.leftHandColor";
    private const string StartColorKey = "app.circuitDefaults.startColor";
    private const string TopColorKey = "app.circuitDefaults.topColor";
    private const string BlinkCountKey = "app.circuitDefaults.blinkCount";
    private const string BlinkPeriodMsKey = "app.circuitDefaults.blinkPeriodMs";
    private const string HoldDurationMsKey = "app.circuitDefaults.holdDurationMs";
    private const string RestBlinkColorKey = "app.trainingVisuals.restBlinkColor";
    private const string RestCompletedColorKey = "app.trainingVisuals.restCompletedColor";
    private const string ResistanceActiveColorKey = "app.trainingVisuals.resistanceActiveColor";
    private const string ResistanceCompletedColorKey = "app.trainingVisuals.resistanceCompletedColor";
    private const string HangActiveColorKey = "app.trainingVisuals.hangActiveColor";
    private const string HangCompletedColorKey = "app.trainingVisuals.hangCompletedColor";

    public AppSettingsDefinition Load()
    {
        return new AppSettingsDefinition
        {
            CircuitDefaults = new CircuitGlobalsDefinition
            {
                PresetName = Preferences.Default.Get(PresetNameKey, "default"),
                Effect = Preferences.Default.Get(EffectKey, "steady"),
                DefaultBrightness = Preferences.Default.Get(DefaultBrightnessKey, 96),
                DimmedBrightness = Preferences.Default.Get(DimmedBrightnessKey, 48),
                RightHandColor = Preferences.Default.Get(RightHandColorKey, "#C44536"),
                LeftHandColor = Preferences.Default.Get(LeftHandColorKey, "#247BA0"),
                StartColor = Preferences.Default.Get(StartColorKey, "#FFFF00"),
                TopColor = Preferences.Default.Get(TopColorKey, "#FF0000"),
                BlinkCount = Preferences.Default.Get(BlinkCountKey, 3),
                BlinkPeriodMs = Preferences.Default.Get(BlinkPeriodMsKey, 250),
                HoldDurationMs = Preferences.Default.Get(HoldDurationMsKey, 2500)
            },
            TrainingVisuals = new TrainingVisualSettingsDefinition
            {
                RestBlinkColor = Preferences.Default.Get(RestBlinkColorKey, "#FF0000"),
                RestCompletedColor = Preferences.Default.Get(RestCompletedColorKey, "#00FF00"),
                ResistanceActiveColor = Preferences.Default.Get(ResistanceActiveColorKey, "#FF8C00"),
                ResistanceCompletedColor = Preferences.Default.Get(ResistanceCompletedColorKey, "#00FF00"),
                HangActiveColor = Preferences.Default.Get(HangActiveColorKey, "#00BFFF"),
                HangCompletedColor = Preferences.Default.Get(HangCompletedColorKey, "#00FF00")
            }
        };
    }

    public void Save(AppSettingsDefinition settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.CircuitDefaults);
        ArgumentNullException.ThrowIfNull(settings.TrainingVisuals);

        Preferences.Default.Set(PresetNameKey, settings.CircuitDefaults.PresetName);
        Preferences.Default.Set(EffectKey, settings.CircuitDefaults.Effect);
        Preferences.Default.Set(DefaultBrightnessKey, settings.CircuitDefaults.DefaultBrightness);
        Preferences.Default.Set(DimmedBrightnessKey, settings.CircuitDefaults.DimmedBrightness);
        Preferences.Default.Set(RightHandColorKey, settings.CircuitDefaults.RightHandColor);
        Preferences.Default.Set(LeftHandColorKey, settings.CircuitDefaults.LeftHandColor);
        Preferences.Default.Set(StartColorKey, settings.CircuitDefaults.StartColor);
        Preferences.Default.Set(TopColorKey, settings.CircuitDefaults.TopColor);
        Preferences.Default.Set(BlinkCountKey, settings.CircuitDefaults.BlinkCount);
        Preferences.Default.Set(BlinkPeriodMsKey, settings.CircuitDefaults.BlinkPeriodMs);
        Preferences.Default.Set(HoldDurationMsKey, settings.CircuitDefaults.HoldDurationMs);
        Preferences.Default.Set(RestBlinkColorKey, settings.TrainingVisuals.RestBlinkColor);
        Preferences.Default.Set(RestCompletedColorKey, settings.TrainingVisuals.RestCompletedColor);
        Preferences.Default.Set(ResistanceActiveColorKey, settings.TrainingVisuals.ResistanceActiveColor);
        Preferences.Default.Set(ResistanceCompletedColorKey, settings.TrainingVisuals.ResistanceCompletedColor);
        Preferences.Default.Set(HangActiveColorKey, settings.TrainingVisuals.HangActiveColor);
        Preferences.Default.Set(HangCompletedColorKey, settings.TrainingVisuals.HangCompletedColor);
    }
}
