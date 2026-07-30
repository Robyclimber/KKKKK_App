namespace RouteLab.Models;

public sealed class PanelImageEditorHandle
{
    public required string Id { get; init; }

    public required PointF NormalizedPosition { get; set; }
}
