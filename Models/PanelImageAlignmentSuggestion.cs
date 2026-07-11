namespace WallPanelPlanner.Models;

public sealed class PanelImageAlignmentSuggestion
{
    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public double Scale { get; init; }

    public string Reason { get; init; } = string.Empty;

    public double Confidence { get; init; }
}
