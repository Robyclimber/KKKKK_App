namespace RouteLab.Models;

public sealed class PanelConfigurationFile
{
    public required string Name { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public double HorizontalSpacing { get; init; }

    public double VerticalSpacing { get; init; }

    public double EdgeOffset { get; init; }
}

