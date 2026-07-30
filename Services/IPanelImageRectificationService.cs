using RouteLab.Models;

namespace RouteLab.Services;

public interface IPanelImageRectificationService
{
    Task<PanelImageRectificationResult> GenerateAsync(
        string sourceImagePath,
        IReadOnlyList<Point> sourceCorners,
        double panelWidth,
        double panelHeight,
        CancellationToken cancellationToken = default);
}
