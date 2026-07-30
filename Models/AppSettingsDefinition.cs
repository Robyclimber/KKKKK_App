namespace RuoteLab.Models;

public sealed class AppSettingsDefinition
{
    public CircuitGlobalsDefinition CircuitDefaults { get; set; } = new();

    public TrainingVisualSettingsDefinition TrainingVisuals { get; set; } = new();
}
