using Microsoft.Maui.Graphics.Platform;
using RouteLab.Models;

namespace RouteLab.Drawing;

public sealed class PanelHoleGridDrawable : IDrawable
{
    private Microsoft.Maui.Graphics.IImage? loadedImage;
    private string? loadedImagePath;

    public required Func<PanelHoleGridState> GridStateProvider { get; init; }

    public required Func<PanelImageCanvasTransformState> TransformProvider { get; init; }

    public string? ImagePath { get; set; }

    public RectF LastPanelRect { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        try
        {
            LastErrorMessage = null;
            DrawCore(canvas, dirtyRect);
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            LastPanelRect = RectF.Zero;
            canvas.SaveState();
            canvas.FillColor = Color.FromArgb("#101820");
            canvas.FillRectangle(dirtyRect);
            DrawCenteredMessage(canvas, dirtyRect, "Immagine non disponibile");
            canvas.RestoreState();
        }
    }

    private void DrawCore(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.FillColor = Color.FromArgb("#101820");
        canvas.FillRectangle(dirtyRect);

        var state = GridStateProvider();
        if (state.PanelWidth <= 0d || state.PanelHeight <= 0d)
        {
            DrawCenteredMessage(canvas, dirtyRect, "Dimensioni pannello non valide");
            canvas.RestoreState();
            return;
        }

        var transform = TransformProvider();
        canvas.Translate(transform.OffsetX, transform.OffsetY);
        canvas.Scale(transform.Scale, transform.Scale);

        var panelRect = CalculatePanelRect(dirtyRect, state.PanelWidth, state.PanelHeight);
        LastPanelRect = panelRect;
        DrawPanelImage(canvas, panelRect);
        DrawGrid(canvas, panelRect, state, transform.Scale);
        canvas.RestoreState();
    }

    private void DrawPanelImage(ICanvas canvas, RectF panelRect)
    {
        canvas.FillColor = Color.FromArgb("#202A31");
        canvas.FillRectangle(panelRect);

        if (string.IsNullOrWhiteSpace(ImagePath) || !File.Exists(ImagePath))
        {
            canvas.FontColor = Color.FromArgb("#B9AA79");
            canvas.FontSize = 14f;
            canvas.DrawString(
                "Nessuna immagine: anteprima sul pannello",
                panelRect,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
            return;
        }

        if (loadedImagePath != ImagePath || loadedImage is null)
        {
            using var stream = File.OpenRead(ImagePath);
            loadedImage = PlatformImage.FromStream(stream);
            loadedImagePath = ImagePath;
        }

        if (loadedImage is not null)
        {
            canvas.DrawImage(loadedImage, panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height);
        }
    }

    private static void DrawGrid(
        ICanvas canvas,
        RectF panelRect,
        PanelHoleGridState state,
        float transformScale)
    {
        var xCoordinates = BuildCoordinates(state.OffsetX, state.PanelWidth, state.SpacingX);
        var yCoordinates = BuildCoordinates(state.OffsetY, state.PanelHeight, state.SpacingY);
        var safeScale = Math.Max(1f, transformScale);

        canvas.SaveState();
        canvas.ClipRectangle(panelRect);
        canvas.StrokeColor = Color.FromRgba("#F2C94C88");
        canvas.StrokeSize = 1.2f / safeScale;

        foreach (var x in xCoordinates)
        {
            var canvasX = panelRect.Left + ((float)(x / state.PanelWidth) * panelRect.Width);
            canvas.DrawLine(canvasX, panelRect.Top, canvasX, panelRect.Bottom);
        }

        foreach (var y in yCoordinates)
        {
            var canvasY = panelRect.Top + ((float)(y / state.PanelHeight) * panelRect.Height);
            canvas.DrawLine(panelRect.Left, canvasY, panelRect.Right, canvasY);
        }

        if ((long)xCoordinates.Count * yCoordinates.Count <= 5000)
        {
            var radius = 5f / safeScale;
            foreach (var x in xCoordinates)
            {
                var canvasX = panelRect.Left + ((float)(x / state.PanelWidth) * panelRect.Width);
                foreach (var y in yCoordinates)
                {
                    var canvasY = panelRect.Top + ((float)(y / state.PanelHeight) * panelRect.Height);
                    canvas.FillColor = Color.FromArgb("#F2C94C");
                    canvas.FillCircle(canvasX, canvasY, radius);
                    canvas.StrokeColor = Color.FromArgb("#111111");
                    canvas.StrokeSize = 1.2f / safeScale;
                    canvas.DrawCircle(canvasX, canvasY, radius);
                }
            }
        }

        canvas.RestoreState();
        canvas.StrokeColor = Color.FromArgb("#F2C94C");
        canvas.StrokeSize = 2f / safeScale;
        canvas.DrawRectangle(panelRect);
    }

    private static IReadOnlyList<double> BuildCoordinates(double offset, double size, double spacing)
    {
        if (offset < 0d || size <= 0d || spacing <= 0d || offset > size / 2d)
        {
            return Array.Empty<double>();
        }

        var coordinates = new List<double>();
        var maximum = size - offset;
        for (var coordinate = offset; coordinate <= maximum + 0.0001d; coordinate += spacing)
        {
            coordinates.Add(coordinate);
            if (coordinates.Count >= 200)
            {
                break;
            }
        }

        return coordinates;
    }

    private static RectF CalculatePanelRect(RectF dirtyRect, double panelWidth, double panelHeight)
    {
        const float margin = 18f;
        var availableWidth = Math.Max(1f, dirtyRect.Width - (margin * 2f));
        var availableHeight = Math.Max(1f, dirtyRect.Height - (margin * 2f));
        var scale = Math.Min(
            availableWidth / (float)panelWidth,
            availableHeight / (float)panelHeight);
        var width = (float)panelWidth * scale;
        var height = (float)panelHeight * scale;
        return new RectF(
            dirtyRect.Left + ((dirtyRect.Width - width) / 2f),
            dirtyRect.Top + ((dirtyRect.Height - height) / 2f),
            width,
            height);
    }

    private static void DrawCenteredMessage(ICanvas canvas, RectF dirtyRect, string message)
    {
        canvas.FontColor = Colors.White;
        canvas.FontSize = 16f;
        canvas.DrawString(message, dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
