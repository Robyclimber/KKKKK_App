namespace RuoteLab.Models;

public sealed class CircuitGlobalsDefinition
{
    public string PresetName { get; set; } = "default";

    public string Effect { get; set; } = "steady";

    public int DefaultBrightness { get; set; } = 96;

    public int DimmedBrightness { get; set; } = 48;

    public string RightHandColor { get; set; } = "#C44536";

    public string LeftHandColor { get; set; } = "#247BA0";

    public string StartColor { get; set; } = "#FFFF00";

    public string TopColor { get; set; } = "#FF0000";

    public int BlinkCount { get; set; } = 3;

    public int BlinkPeriodMs { get; set; } = 250;

    public int HoldDurationMs { get; set; } = 2500;
}
