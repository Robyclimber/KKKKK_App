namespace RuoteLab.Models;

public sealed class AppSettingsDefinition
{
    public CircuitGlobalsDefinition CircuitDefaults { get; set; } = new();
}
