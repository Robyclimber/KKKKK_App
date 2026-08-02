using Microsoft.Maui.Graphics;
using RouteLab.Models;

namespace RouteLab.Drawing;

public sealed class LayoutPreviewDrawable : IDrawable
{
    private const float Padding = 24f;

    public WallDefinition? Wall { get; set; }

    public PanelDefinition? SelectedPanel { get; set; }

    public float PixelsPerMillimeter { get; set; } = 0.1f;

    public float ZoomFactor { get; set; } = 1f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.FillColor = Color.FromArgb("#12100C");
        canvas.FillRectangle(dirtyRect);

        if (Wall is null || Wall.Width <= 0 || Wall.Height <= 0)
        {
            DrawPlaceholder(canvas, dirtyRect);
            canvas.RestoreState();
            return;
        }

        var scale = Math.Max(0.01f, PixelsPerMillimeter * ZoomFactor);
        var wallWidth = (float)Wall.Width * scale;
        var wallHeight = (float)Wall.Height * scale;
        var originX = Padding;
        var originY = Padding;

        canvas.FillColor = Color.FromArgb("#191611");
        canvas.FillRoundedRectangle(originX, originY, wallWidth, wallHeight, 12f);
        canvas.StrokeColor = Color.FromArgb("#F2C94C");
        canvas.StrokeSize = 3f;
        canvas.DrawRoundedRectangle(originX, originY, wallWidth, wallHeight, 12f);

        var orderedWallHoles = Wall.GetOrderedHoles();

        foreach (var panel in Wall.Panels)
        {
            var panelX = originX + (float)panel.X * scale;
            var panelY = originY + (float)panel.Y * scale;
            var panelWidth = (float)panel.Width * scale;
            var panelHeight = (float)panel.Height * scale;
            var isSelected = ReferenceEquals(panel, SelectedPanel);

            canvas.FillColor = isSelected
                ? Color.FromArgb("#2A2212")
                : Color.FromArgb("#211C14");
            canvas.FillRectangle(panelX, panelY, panelWidth, panelHeight);
            canvas.StrokeColor = isSelected
                ? Color.FromArgb("#F2C94C")
                : Color.FromArgb("#B9922F");
            canvas.StrokeSize = isSelected ? 4f : 2f;
            canvas.DrawRectangle(panelX, panelY, panelWidth, panelHeight);

            canvas.FontColor = Color.FromArgb("#F8E7A8");
            canvas.FontSize = 12f;
            canvas.DrawString(panel.Name, panelX + 6f, panelY + 6f, panelWidth - 12f, 18f, HorizontalAlignment.Left, VerticalAlignment.Top);

            canvas.FillColor = Color.FromArgb("#F2C94C");
            foreach (var hole in panel.GetHolePositions())
            {
                var holeX = panelX + (float)hole.X * scale;
                var holeY = panelY + (float)hole.Y * scale;
                var radius = Math.Max(2f, scale * 1.8f);
                canvas.FillCircle(holeX, holeY, radius);
            }

            if (isSelected && ZoomFactor >= 1.5f)
            {
                canvas.FontColor = Color.FromArgb("#F2C94C");
                canvas.FontSize = Math.Max(9f, scale * 3.2f);

                foreach (var hole in orderedWallHoles.Where(hole =>
                             hole.PanelName == panel.Name &&
                             Math.Abs(hole.PanelX - panel.X) < 0.0001d &&
                             Math.Abs(hole.PanelY - panel.Y) < 0.0001d))
                {
                    var holeX = panelX + (float)hole.RelativeX * scale;
                    var holeY = panelY + (float)hole.RelativeY * scale;
                    canvas.DrawString(
                        hole.Number.ToString(),
                        holeX + 4f,
                        holeY - 10f,
                        28f,
                        14f,
                        HorizontalAlignment.Left,
                        VerticalAlignment.Center);
                }
            }
        }

        canvas.FontColor = Color.FromArgb("#F8E7A8");
        canvas.FontSize = 13f;
        canvas.DrawString($"{Wall.Name} - {Wall.Width:0.#} x {Wall.Height:0.#} mm", originX, 4f, wallWidth, 18f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.RestoreState();
    }

    public Size GetDesiredSize(double zoom)
    {
        if (Wall is null || Wall.Width <= 0 || Wall.Height <= 0)
        {
            return new Size(320, 320);
        }

        var scale = Math.Max(0.01, PixelsPerMillimeter * zoom);
        var width = (Wall.Width * scale) + Padding * 2;
        var height = (Wall.Height * scale) + Padding * 2;
        return new Size(width, height);
    }

    public RectF GetWallBounds()
    {
        if (Wall is null || Wall.Width <= 0 || Wall.Height <= 0)
        {
            return new RectF(Padding, Padding, 0, 0);
        }

        var scale = Math.Max(0.01f, PixelsPerMillimeter * ZoomFactor);
        return new RectF(
            Padding,
            Padding,
            (float)Wall.Width * scale,
            (float)Wall.Height * scale);
    }

    private static void DrawPlaceholder(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Color.FromArgb("#D8A72D");
        canvas.FontSize = 16f;
        canvas.DrawString(
            "Crea una parete e aggiungi i pannelli per vedere l'anteprima.",
            dirtyRect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }
}

