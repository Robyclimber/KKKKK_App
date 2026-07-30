namespace WallPanelPlanner.Models;

public sealed class PanelInput
{
    public required string Name { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public double HorizontalSpacing { get; init; }

    public double VerticalSpacing { get; init; }

    public double EdgeOffsetX { get; init; }

    public double EdgeOffsetY { get; init; }
}
