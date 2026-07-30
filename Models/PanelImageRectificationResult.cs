namespace RouteLab.Models;

public sealed record PanelImageRectificationResult(
    bool IsSuccess,
    string Message,
    string? FilePath,
    int PixelWidth,
    int PixelHeight)
{
    public static PanelImageRectificationResult Failure(string message) =>
        new(false, message, null, 0, 0);
}
