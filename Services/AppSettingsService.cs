using Microsoft.Maui.Storage;
using RouteLab.Models;

namespace RouteLab.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private const string ClimberHeightMmKey = "app.climber.heightMm";
    private const string ClimberArmSpanMmKey = "app.climber.armSpanMm";
    private const string ClimberMassKgKey = "app.climber.massKg";
    private const string ClimberBodyDistanceFromWallMmKey = "app.climber.bodyDistanceFromWallMm";
    private const string ClimberProfilesKey = "app.climber.profiles";
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
    private readonly IBusyIndicatorService? busyIndicatorService;

    public AppSettingsService(IBusyIndicatorService? busyIndicatorService = null)
    {
        this.busyIndicatorService = busyIndicatorService;
    }

    public AppSettingsDefinition Load()
    {
        return Execute("Caricamento impostazioni...", () =>
        {
            var legacyDefaultProfile = new ClimberProfileDefinition
            {
                HeightMm = Preferences.Default.Get(ClimberHeightMmKey, 1750d),
                ArmSpanMm = Preferences.Default.Get(ClimberArmSpanMmKey, 1750d),
                MassKg = Preferences.Default.Get(ClimberMassKgKey, 70d),
                BodyDistanceFromWallMm = Preferences.Default.Get(ClimberBodyDistanceFromWallMmKey, 180d)
            };

            return new AppSettingsDefinition
            {
                ClimberProfiles = LoadClimberProfiles(legacyDefaultProfile),
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
        });
    }

    public void Save(AppSettingsDefinition settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.ClimberProfiles);
        ArgumentNullException.ThrowIfNull(settings.CircuitDefaults);
        ArgumentNullException.ThrowIfNull(settings.TrainingVisuals);
        Execute("Salvataggio impostazioni...", () =>
        {
            var profiles = NormalizeClimberProfiles(settings.ClimberProfiles, new ClimberProfileDefinition());
            var defaultProfile = profiles.First(profile => profile.IsDefault);
            Preferences.Default.Set(ClimberProfilesKey, System.Text.Json.JsonSerializer.Serialize(profiles));
            Preferences.Default.Set(ClimberHeightMmKey, defaultProfile.HeightMm);
            Preferences.Default.Set(ClimberArmSpanMmKey, defaultProfile.ArmSpanMm);
            Preferences.Default.Set(ClimberMassKgKey, defaultProfile.MassKg);
            Preferences.Default.Set(ClimberBodyDistanceFromWallMmKey, defaultProfile.BodyDistanceFromWallMm);
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
        });
    }

    private T Execute<T>(string message, Func<T> action)
    {
        return busyIndicatorService is null
            ? action()
            : busyIndicatorService.Run(message, action);
    }

    private void Execute(string message, Action action)
    {
        if (busyIndicatorService is null)
        {
            action();
            return;
        }

        busyIndicatorService.Run(message, action);
    }

    private static List<ClimberProfileDefinition> LoadClimberProfiles(ClimberProfileDefinition legacyDefaultProfile)
    {
        var serializedProfiles = Preferences.Default.Get(ClimberProfilesKey, string.Empty);
        if (string.IsNullOrWhiteSpace(serializedProfiles))
        {
            return [legacyDefaultProfile];
        }

        try
        {
            var savedProfiles = System.Text.Json.JsonSerializer.Deserialize<List<ClimberProfileDefinition>>(serializedProfiles);
            return NormalizeClimberProfiles(savedProfiles, legacyDefaultProfile);
        }
        catch (System.Text.Json.JsonException)
        {
            return [legacyDefaultProfile];
        }
    }

    private static List<ClimberProfileDefinition> NormalizeClimberProfiles(
        IEnumerable<ClimberProfileDefinition>? profiles,
        ClimberProfileDefinition fallbackDefault)
    {
        var normalized = new List<ClimberProfileDefinition>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var savedDefault = profiles?.FirstOrDefault(profile => profile?.IsDefault == true);
        var defaultProfile = (savedDefault ?? fallbackDefault).Clone();
        defaultProfile.Id = ClimberProfileDefinition.DefaultProfileId;
        defaultProfile.Name = "Persona predefinita";
        normalized.Add(defaultProfile);
        usedIds.Add(defaultProfile.Id);

        foreach (var source in profiles ?? Array.Empty<ClimberProfileDefinition>())
        {
            if (source is null || source.IsDefault)
            {
                continue;
            }

            var profile = source.Clone();
            profile.Id = string.IsNullOrWhiteSpace(profile.Id)
                ? Guid.NewGuid().ToString("N")
                : profile.Id.Trim();
            profile.Name = string.IsNullOrWhiteSpace(profile.Name)
                ? $"Atleta {normalized.Count}"
                : profile.Name.Trim();
            if (!usedIds.Add(profile.Id))
            {
                profile.Id = Guid.NewGuid().ToString("N");
                usedIds.Add(profile.Id);
            }

            normalized.Add(profile);
        }

        return normalized;
    }
}

