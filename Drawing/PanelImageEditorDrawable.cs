using RouteLab.Models;
using Microsoft.Maui.Graphics.Platform;

namespace RouteLab.Drawing;

public sealed class PanelImageEditorDrawable : IDrawable
{
    private Microsoft.Maui.Graphics.IImage? loadedImage;
    private string? loadedImagePath;
    public string? LastErrorMessage { get; private set; }
    public const float HandleRadius = 12f;
    private const float MagnifierRadius = 56f;
    private const float MagnifierZoom = 2.4f;

    public required Func<IReadOnlyList<PanelImageEditorHandle>> HandlesProvider { get; init; }

    public required Func<PanelImageCanvasTransformState> TransformProvider { get; init; }

    public string? ImagePath { get; set; }

    public string? ActiveHandleId { get; set; }

    public RectF LastImageRect { get; private set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        try
        {
            LastErrorMessage = null;
            canvas.SaveState();
            canvas.FillColor = Color.FromArgb("#101820");
            canvas.FillRectangle(dirtyRect);

            var transform = TransformProvider();
            canvas.Translate(transform.OffsetX, transform.OffsetY);
            canvas.Scale(transform.Scale, transform.Scale);

            var imageRect = DrawImage(canvas, dirtyRect);
            LastImageRect = imageRect;
            if (imageRect.Width > 0 && imageRect.Height > 0)
            {
                DrawOverlay(canvas, imageRect, HandlesProvider());
            }

            canvas.RestoreState();

            if (imageRect.Width > 0 && imageRect.Height > 0)
            {
                DrawMagnifier(canvas, dirtyRect, imageRect, HandlesProvider(), transform);
            }
        }
        catch (Exception ex)
        {
            LastImageRect = RectF.Zero;
            LastErrorMessage = ex.Message;
            canvas.SaveState();
            canvas.FillColor = Color.FromArgb("#101820");
            canvas.FillRectangle(dirtyRect);
            canvas.FontColor = Colors.White;
            canvas.FontSize = 16;
            canvas.DrawString("Errore editor immagine", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.RestoreState();
        }
    }

    private RectF DrawImage(ICanvas canvas, RectF dirtyRect)
    {
        if (string.IsNullOrWhiteSpace(ImagePath) || !File.Exists(ImagePath))
        {
            canvas.FontColor = Colors.White;
            canvas.FontSize = 18;
            canvas.DrawString("Nessuna immagine disponibile", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return RectF.Zero;
        }

        if (loadedImagePath != ImagePath || loadedImage is null)
        {
            using var stream = File.OpenRead(ImagePath);
            loadedImage = PlatformImage.FromStream(stream);
            loadedImagePath = ImagePath;
        }

        if (loadedImage is null)
        {
            return RectF.Zero;
        }

        var scale = Math.Min(dirtyRect.Width / loadedImage.Width, dirtyRect.Height / loadedImage.Height);
        var width = loadedImage.Width * scale;
        var height = loadedImage.Height * scale;
        var left = (dirtyRect.Width - width) / 2f;
        var top = (dirtyRect.Height - height) / 2f;
        var imageRect = new RectF(left, top, width, height);

        canvas.DrawImage(loadedImage, imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height);
        return imageRect;
    }

    private static void DrawOverlay(ICanvas canvas, RectF imageRect, IReadOnlyList<PanelImageEditorHandle> handles)
    {
        if (handles.Count != 4)
        {
            return;
        }

        var points = handles
            .Select(handle => new PointF(
                imageRect.Left + (handle.NormalizedPosition.X * imageRect.Width),
                imageRect.Top + (handle.NormalizedPosition.Y * imageRect.Height)))
            .ToArray();

        canvas.StrokeColor = Color.FromArgb("#6FE7DD");
        canvas.StrokeSize = 3;
        var path = new PathF();
        path.MoveTo(points[0]);
        path.LineTo(points[1]);
        path.LineTo(points[2]);
        path.LineTo(points[3]);
        path.Close();
        canvas.DrawPath(path);

        foreach (var point in points)
        {
            canvas.FillColor = Color.FromArgb("#FFB703");
            canvas.FillCircle(point, HandleRadius);
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 2;
            canvas.DrawCircle(point, HandleRadius);
        }
    }

    private void DrawMagnifier(
        ICanvas canvas,
        RectF viewportRect,
        RectF imageRect,
        IReadOnlyList<PanelImageEditorHandle> handles,
        PanelImageCanvasTransformState transform)
    {
        if (loadedImage is null || string.IsNullOrWhiteSpace(ActiveHandleId))
        {
            return;
        }

        var activeIndex = handles
            .Select((handle, index) => new { handle.Id, index })
            .FirstOrDefault(item => item.Id == ActiveHandleId)?
            .index;

        if (activeIndex is not int index)
        {
            return;
        }

        var focusPoint = new PointF(
            imageRect.Left + (handles[index].NormalizedPosition.X * imageRect.Width),
            imageRect.Top + (handles[index].NormalizedPosition.Y * imageRect.Height));
        var scale = transform.Scale <= 0f ? 1f : transform.Scale;
        var screenImageRect = new RectF(
            (imageRect.Left * scale) + transform.OffsetX,
            (imageRect.Top * scale) + transform.OffsetY,
            imageRect.Width * scale,
            imageRect.Height * scale);
        var screenFocusPoint = new PointF(
            (focusPoint.X * scale) + transform.OffsetX,
            (focusPoint.Y * scale) + transform.OffsetY);
        const float lensMargin = 12f;
        var viewportLeft = viewportRect.Left + MagnifierRadius + lensMargin;
        var viewportTop = viewportRect.Top + MagnifierRadius + lensMargin;
        var viewportRight = viewportRect.Right - MagnifierRadius - lensMargin;
        var viewportBottom = viewportRect.Bottom - MagnifierRadius - lensMargin;

        var lensCenter = new PointF(
            Math.Clamp(screenFocusPoint.X + 96f, viewportLeft, viewportRight),
            Math.Clamp(screenFocusPoint.Y - 96f, viewportTop, viewportBottom));

        canvas.SaveState();

        var lensPath = new PathF();
        lensPath.AppendCircle(lensCenter.X, lensCenter.Y, MagnifierRadius);
        canvas.ClipPath(lensPath);
        canvas.FillColor = Colors.White;
        canvas.FillCircle(lensCenter, MagnifierRadius);

        var scaledWidth = screenImageRect.Width * MagnifierZoom;
        var scaledHeight = screenImageRect.Height * MagnifierZoom;
        var relativeFocusX = screenFocusPoint.X - screenImageRect.Left;
        var relativeFocusY = screenFocusPoint.Y - screenImageRect.Top;
        var drawX = lensCenter.X - (relativeFocusX * MagnifierZoom);
        var drawY = lensCenter.Y - (relativeFocusY * MagnifierZoom);
        canvas.DrawImage(loadedImage, drawX, drawY, scaledWidth, scaledHeight);

        canvas.StrokeColor = Color.FromArgb("#6FE7DD");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(lensCenter.X - 18f, lensCenter.Y, lensCenter.X + 18f, lensCenter.Y);
        canvas.DrawLine(lensCenter.X, lensCenter.Y - 18f, lensCenter.X, lensCenter.Y + 18f);
        canvas.FillColor = Color.FromArgb("#FFB703");
        canvas.FillCircle(lensCenter, 8f);
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(lensCenter, 8f);

        canvas.RestoreState();

        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 3f;
        canvas.DrawCircle(lensCenter, MagnifierRadius);
    }
}
