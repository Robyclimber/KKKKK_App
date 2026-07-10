namespace WallPanelPlanner.Models;

public sealed class WallConfigurationFile
{
    public required string Name { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public string? ImagePath { get; init; }

    public double ImageOffsetX { get; init; }

    public double ImageOffsetY { get; init; }

    public double ImageScale { get; init; }

    public double ImageOpacity { get; init; }

    public required List<PanelConfigurationFile> Panels { get; init; }
}
