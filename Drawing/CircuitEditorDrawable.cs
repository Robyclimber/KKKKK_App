using Microsoft.Maui.Graphics;
using RuoteLab.Models;

namespace RuoteLab.Drawing;

public sealed class CircuitEditorDrawable : IDrawable
{
    private const float Padding = 24f;

    public WallDefinition? Wall { get; set; }

    public CircuitDefinition? Circuit { get; set; }

    public WallHoleDefinition? HighlightedHole { get; set; }

    public IReadOnlyList<WallHoleDefinition> SelectedHoles { get; set; } = Array.Empty<WallHoleDefinition>();

    public WallHoleDefinition? SuggestedHole { get; set; }

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

        foreach (var panel in Wall.Panels)
        {
            var panelX = originX + (float)panel.X * scale;
            var panelY = originY + (float)panel.Y * scale;
            var panelWidth = (float)panel.Width * scale;
            var panelHeight = (float)panel.Height * scale;

            canvas.FillColor = Color.FromArgb("#211C14");
            canvas.FillRectangle(panelX, panelY, panelWidth, panelHeight);
            canvas.StrokeColor = Color.FromArgb("#B9922F");
            canvas.StrokeSize = 2f;
            canvas.DrawRectangle(panelX, panelY, panelWidth, panelHeight);

            canvas.FontColor = Color.FromArgb("#F8E7A8");
            canvas.FontSize = 11f;
            canvas.DrawString(panel.Name, panelX + 4f, panelY + 4f, panelWidth - 8f, 16f, HorizontalAlignment.Left, VerticalAlignment.Top);
        }

        var holes = Wall.GetOrderedHoles();
        foreach (var hole in holes)
        {
            var holeX = originX + (float)hole.AbsoluteX * scale;
            var holeY = originY + (float)hole.AbsoluteY * scale;

            canvas.FillColor = Color.FromArgb("#F2C94C");
            canvas.FillCircle(holeX, holeY, Math.Max(2.5f, scale * 1.9f));

            if (ZoomFactor >= 1.2f)
            {
                canvas.FontColor = Color.FromArgb("#F2C94C");
                canvas.FontSize = Math.Max(9f, scale * 4.2f);
                var labelWidth = Math.Max(28f, scale * 14f);
                var labelHeight = Math.Max(14f, scale * 6f);
                canvas.DrawString(
                    hole.Number.ToString(),
                    holeX - (labelWidth / 2f),
                    holeY - Math.Max(20f, scale * 9f),
                    labelWidth,
                    labelHeight,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }
        }

        if (HighlightedHole is WallHoleDefinition highlightedHole && highlightedHole.Number > 0)
        {
            var holeX = originX + (float)highlightedHole.AbsoluteX * scale;
            var holeY = originY + (float)highlightedHole.AbsoluteY * scale;

            canvas.StrokeColor = Color.FromArgb("#FFF1A8");
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(holeX, holeY, Math.Max(11f, scale * 6.5f));

            canvas.StrokeColor = Color.FromArgb("#F2C94C");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(holeX, holeY, Math.Max(7.5f, scale * 4.5f));
        }

        if (SelectedHoles.Count > 0)
        {
            foreach (var selectedHole in SelectedHoles.Where(hole => hole.Number > 0))
            {
                var holeX = originX + (float)selectedHole.AbsoluteX * scale;
                var holeY = originY + (float)selectedHole.AbsoluteY * scale;

                canvas.FillColor = Color.FromArgb("#00BFFF").WithAlpha(0.22f);
                canvas.FillCircle(holeX, holeY, Math.Max(10f, scale * 5.8f));
                canvas.StrokeColor = Color.FromArgb("#7FDBFF");
                canvas.StrokeSize = 3f;
                canvas.DrawCircle(holeX, holeY, Math.Max(8f, scale * 4.8f));
            }
        }

        if (SuggestedHole is WallHoleDefinition suggestedHole && suggestedHole.Number > 0)
        {
            var holeX = originX + (float)suggestedHole.AbsoluteX * scale;
            var holeY = originY + (float)suggestedHole.AbsoluteY * scale;

            canvas.FillColor = Color.FromArgb("#39FF88").WithAlpha(0.24f);
            canvas.FillCircle(holeX, holeY, Math.Max(12f, scale * 6.2f));
            canvas.StrokeColor = Color.FromArgb("#A6FFC8");
            canvas.StrokeSize = 4f;
            canvas.DrawCircle(holeX, holeY, Math.Max(9f, scale * 5.2f));
            canvas.StrokeColor = Color.FromArgb("#39FF88");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(holeX, holeY, Math.Max(14f, scale * 7.4f));
        }

        if (Circuit is not null)
        {
            var groupedMovements = Circuit.Movements
                .Where(movement => movement.WallName == Wall.Name)
                .GroupBy(movement => movement.HoleNumber);

            foreach (var movementGroup in groupedMovements)
            {
                var hole = holes.FirstOrDefault(item => item.Number == movementGroup.Key);
                if (hole.Number == 0)
                {
                    continue;
                }

                var holeX = originX + (float)hole.AbsoluteX * scale;
                var holeY = originY + (float)hole.AbsoluteY * scale;
                DrawMovementGroup(canvas, movementGroup.ToList(), holeX, holeY, scale);
            }
        }

        canvas.RestoreState();
    }

    public Size GetDesiredSize(double zoom)
    {
        if (Wall is null || Wall.Width <= 0 || Wall.Height <= 0)
        {
            return new Size(320, 320);
        }

        var scale = Math.Max(0.01, PixelsPerMillimeter * zoom);
        return new Size((Wall.Width * scale) + Padding * 2, (Wall.Height * scale) + Padding * 2);
    }

    public RectF GetWallBounds()
    {
        if (Wall is null || Wall.Width <= 0 || Wall.Height <= 0)
        {
            return new RectF(Padding, Padding, 0, 0);
        }

        var scale = Math.Max(0.01f, PixelsPerMillimeter * ZoomFactor);
        return new RectF(Padding, Padding, (float)Wall.Width * scale, (float)Wall.Height * scale);
    }

    public WallHoleDefinition? FindNearestHole(Point tapPoint, double tolerancePixels = 22)
    {
        if (Wall is null)
        {
            return null;
        }

        var scale = Math.Max(0.01f, PixelsPerMillimeter * ZoomFactor);
        var originX = Padding;
        var originY = Padding;

        var nearest = Wall.GetOrderedHoles()
            .Select(hole => new
            {
                Hole = hole,
                X = originX + (float)hole.AbsoluteX * scale,
                Y = originY + (float)hole.AbsoluteY * scale
            })
            .Select(item => new
            {
                item.Hole,
                Distance = Math.Sqrt(Math.Pow(item.X - tapPoint.X, 2) + Math.Pow(item.Y - tapPoint.Y, 2))
            })
            .OrderBy(item => item.Distance)
            .FirstOrDefault();

        if (nearest is null || nearest.Distance > tolerancePixels)
        {
            return null;
        }

        return nearest.Hole;
    }

    private static void DrawPlaceholder(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Color.FromArgb("#D8A72D");
        canvas.FontSize = 16f;
        canvas.DrawString(
            "Seleziona una parete e crea un circuito per iniziare.",
            dirtyRect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static Color GetMovementColor(CircuitMovementDefinition movement)
    {
        return movement.Role switch
        {
            MovementRole.Start => Color.FromArgb("#2E8B57"),
            MovementRole.Top => Color.FromArgb("#F2C94C"),
            _ => movement.Hand == HandSide.Left
                ? Color.FromArgb("#247BA0")
                : Color.FromArgb("#C44536")
        };
    }

    private static void DrawMovementGroup(ICanvas canvas, IReadOnlyList<CircuitMovementDefinition> movements, float holeX, float holeY, float scale)
    {
        var hasStart = movements.Any(movement => movement.Role == MovementRole.Start);
        var hasTop = movements.Any(movement => movement.Role == MovementRole.Top);

        if (hasStart)
        {
            canvas.FillColor = Color.FromArgb("#2E8B57").WithAlpha(0.18f);
            canvas.FillCircle(holeX, holeY, Math.Max(9f, scale * 5.1f));
        }

        if (hasTop)
        {
            canvas.StrokeColor = Color.FromArgb("#F2C94C");
            canvas.StrokeSize = 5f;
            canvas.DrawCircle(holeX, holeY, Math.Max(9.5f, scale * 5.4f));
        }
        else
        {
            canvas.StrokeColor = Color.FromArgb("#B9922F");
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(holeX, holeY, Math.Max(7f, scale * 4.2f));
        }

        var orderedMovements = movements
            .OrderBy(movement => movement.Sequence)
            .ThenBy(movement => movement.Role)
            .ThenBy(movement => movement.Hand)
            .ToList();

        for (var index = 0; index < orderedMovements.Count; index++)
        {
            DrawMovementTag(canvas, orderedMovements[index], holeX, holeY, scale, index, orderedMovements.Count);
        }
    }

    private static void DrawMovementTag(ICanvas canvas, CircuitMovementDefinition movement, float holeX, float holeY, float scale, int index, int totalCount)
    {
        var color = GetMovementColor(movement);
        var text = movement.Role switch
        {
            MovementRole.Start => movement.Hand == HandSide.Right ? $"SDX {movement.Sequence:00}" : $"SSX {movement.Sequence:00}",
            MovementRole.Top => movement.Hand == HandSide.Right ? $"TDX {movement.Sequence:00}" : $"TSX {movement.Sequence:00}",
            _ => movement.Hand == HandSide.Right ? $"DX {movement.Sequence:00}" : $"SX {movement.Sequence:00}"
        };

        var spacingY = Math.Max(12f, scale * 5.6f);
        var tagHeight = Math.Max(11f, scale * 4.8f);
        var tagWidth = Math.Max(34f, scale * 18f);
        var baseX = holeX + Math.Max(10f, scale * 5.2f);
        var startY = holeY - (((totalCount - 1) * spacingY) / 2f);
        var tagY = startY + (index * spacingY);

        canvas.FillColor = color;
        canvas.FillRoundedRectangle(baseX, tagY - (tagHeight / 2f), tagWidth, tagHeight, 6f);
        canvas.StrokeColor = Color.FromArgb("#12100C");
        canvas.StrokeSize = 1.2f;
        canvas.DrawRoundedRectangle(baseX, tagY - (tagHeight / 2f), tagWidth, tagHeight, 6f);
        canvas.FontColor = movement.Role == MovementRole.Top ? Color.FromArgb("#14110B") : Color.FromArgb("#F8E7A8");
        canvas.FontSize = Math.Max(7.5f, scale * 2.5f);
        canvas.DrawString(text, baseX + 4f, tagY - (tagHeight / 2f), tagWidth - 8f, tagHeight, HorizontalAlignment.Left, VerticalAlignment.Center);
    }
}
