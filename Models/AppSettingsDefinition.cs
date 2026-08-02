namespace RouteLab.Models;

public sealed class AppSettingsDefinition
{
    public List<ClimberProfileDefinition> ClimberProfiles { get; set; } =
    [
        new()
    ];

    public CircuitGlobalsDefinition CircuitDefaults { get; set; } = new();

    public TrainingVisualSettingsDefinition TrainingVisuals { get; set; } = new();

    public ClimberProfileDefinition ResolveClimberProfile(string? profileId)
    {
        var selected = ClimberProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        return selected
               ?? ClimberProfiles.FirstOrDefault(profile => profile.IsDefault)
               ?? new ClimberProfileDefinition();
    }
}

