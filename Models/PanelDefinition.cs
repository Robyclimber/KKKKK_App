using System.Collections.ObjectModel;

namespace RuoteLab.Models;

public sealed class PanelDefinition
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

    public LedRoutingAxis LedRoutingAxis { get; init; } = LedRoutingAxis.Vertical;

    public LedStartDirection LedStartDirection { get; init; } = LedStartDirection.BottomToTop;

    public string? ImagePath { get; set; }

    public double ImageOffsetX { get; set; }

    public double ImageOffsetY { get; set; }

    public double ImageScale { get; set; } = 1d;

    public double ImageOpacity { get; set; } = 0.55d;

    public double ImageCropLeft { get; set; }

    public double ImageCropTop { get; set; }

    public double ImageCropRight { get; set; }

    public double ImageCropBottom { get; set; }

    public double ImagePerspectiveTopLeftX { get; set; }

    public double ImagePerspectiveTopLeftY { get; set; }

    public double ImagePerspectiveTopRightX { get; set; } = 1d;

    public double ImagePerspectiveTopRightY { get; set; }

    public double ImagePerspectiveBottomLeftX { get; set; }

    public double ImagePerspectiveBottomLeftY { get; set; } = 1d;

    public double ImagePerspectiveBottomRightX { get; set; } = 1d;

    public double ImagePerspectiveBottomRightY { get; set; } = 1d;

    public double EffectiveImageCropLeft => ClampCrop(ImageCropLeft);

    public double EffectiveImageCropTop => ClampCrop(ImageCropTop);

    public double EffectiveImageCropRight => ClampCrop(ImageCropRight);

    public double EffectiveImageCropBottom => ClampCrop(ImageCropBottom);

    public double EffectiveImageCropWidthFactor => Math.Max(0.001d, 1d - EffectiveImageCropLeft - EffectiveImageCropRight);

    public double EffectiveImageCropHeightFactor => Math.Max(0.001d, 1d - EffectiveImageCropTop - EffectiveImageCropBottom);

    public Point EffectivePerspectiveTopLeft => new(ClampUnit(ImagePerspectiveTopLeftX), ClampUnit(ImagePerspectiveTopLeftY));

    public Point EffectivePerspectiveTopRight => new(ClampUnit(ImagePerspectiveTopRightX), ClampUnit(ImagePerspectiveTopRightY));

    public Point EffectivePerspectiveBottomLeft => new(ClampUnit(ImagePerspectiveBottomLeftX), ClampUnit(ImagePerspectiveBottomLeftY));

    public Point EffectivePerspectiveBottomRight => new(ClampUnit(ImagePerspectiveBottomRightX), ClampUnit(ImagePerspectiveBottomRightY));

    public string Summary =>
        $"{Name} - Pos({X:0.#}, {Y:0.#}) mm - {Width:0.#} x {Height:0.#} mm - Fori: {HoleCount} - LED {GetRoutingSummary()}";

    public int HoleCount => GetOrderedHoles().Count;

    public IReadOnlyList<HolePoint> GetHolePositions()
    {
        return GetOrderedHoles()
            .Select(hole => new HolePoint(hole.X, hole.Y))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<HoleDefinition> GetOrderedHoles()
    {
        if (Width <= 0 || Height <= 0 || HorizontalSpacing <= 0 || VerticalSpacing <= 0 || EdgeOffsetX < 0 || EdgeOffsetY < 0)
        {
            return Array.Empty<HoleDefinition>();
        }

        var maxX = Width - EdgeOffsetX;
        var maxY = Height - EdgeOffsetY;

        if (EdgeOffsetX > maxX || EdgeOffsetY > maxY)
        {
            return Array.Empty<HoleDefinition>();
        }

        var xColumns = new List<double>();
        for (var x = EdgeOffsetX; x <= maxX; x += HorizontalSpacing)
        {
            xColumns.Add(x);
        }

        var yRows = new List<double>();
        for (var y = EdgeOffsetY; y <= maxY; y += VerticalSpacing)
        {
            yRows.Add(y);
        }

        var holes = new List<HoleDefinition>();
        var holeNumber = 1;

        ValidateLedRouting();

        if (LedRoutingAxis == LedRoutingAxis.Vertical)
        {
            for (var columnIndex = 0; columnIndex < xColumns.Count; columnIndex++)
            {
                var isEvenGroup = columnIndex % 2 == 0;
                var useBottomToTop = LedStartDirection == LedStartDirection.BottomToTop
                    ? isEvenGroup
                    : !isEvenGroup;
                var ySequence = useBottomToTop
                    ? yRows.AsEnumerable().Reverse()
                    : yRows.AsEnumerable();

                foreach (var y in ySequence)
                {
                    holes.Add(new HoleDefinition(holeNumber++, xColumns[columnIndex], y));
                }
            }
        }
        else
        {
            for (var rowIndex = 0; rowIndex < yRows.Count; rowIndex++)
            {
                var isEvenGroup = rowIndex % 2 == 0;
                var useLeftToRight = LedStartDirection == LedStartDirection.LeftToRight
                    ? isEvenGroup
                    : !isEvenGroup;
                var xSequence = useLeftToRight
                    ? xColumns.AsEnumerable()
                    : xColumns.AsEnumerable().Reverse();

                foreach (var x in xSequence)
                {
                    holes.Add(new HoleDefinition(holeNumber++, x, yRows[rowIndex]));
                }
            }
        }

        return new ReadOnlyCollection<HoleDefinition>(holes);
    }

    private void ValidateLedRouting()
    {
        var isValid = LedRoutingAxis switch
        {
            LedRoutingAxis.Vertical => LedStartDirection is LedStartDirection.BottomToTop or LedStartDirection.TopToBottom,
            LedRoutingAxis.Horizontal => LedStartDirection is LedStartDirection.LeftToRight or LedStartDirection.RightToLeft,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException("La direzione iniziale LED non e' coerente con l'asse scelto.");
        }
    }

    private string GetRoutingSummary()
    {
        return $"{GetAxisLabel(LedRoutingAxis)} {GetDirectionLabel(LedStartDirection)}";
    }

    public static string GetAxisLabel(LedRoutingAxis axis)
    {
        return axis switch
        {
            LedRoutingAxis.Horizontal => "Orizzontale",
            _ => "Verticale"
        };
    }

    public static string GetDirectionLabel(LedStartDirection direction)
    {
        return direction switch
        {
            LedStartDirection.TopToBottom => "alto -> basso",
            LedStartDirection.LeftToRight => "sx -> dx",
            LedStartDirection.RightToLeft => "dx -> sx",
            _ => "basso -> alto"
        };
    }

    private static double ClampCrop(double value)
    {
        return Math.Clamp(value, 0d, 0.999d);
    }

    private static double ClampUnit(double value)
    {
        return Math.Clamp(value, 0d, 1d);
    }

    public Point MapPanelPointToImageSource(double normalizedX, double normalizedY, double sourceWidth, double sourceHeight)
    {
        var u = ClampUnit(normalizedX);
        var v = ClampUnit(normalizedY);
        var cropLeftPx = sourceWidth * EffectiveImageCropLeft;
        var cropTopPx = sourceHeight * EffectiveImageCropTop;
        var cropWidthPx = sourceWidth * EffectiveImageCropWidthFactor;
        var cropHeightPx = sourceHeight * EffectiveImageCropHeightFactor;

        var topLeft = EffectivePerspectiveTopLeft;
        var topRight = EffectivePerspectiveTopRight;
        var bottomLeft = EffectivePerspectiveBottomLeft;
        var bottomRight = EffectivePerspectiveBottomRight;

        var sourceX =
            ((1d - u) * (1d - v) * topLeft.X) +
            (u * (1d - v) * topRight.X) +
            ((1d - u) * v * bottomLeft.X) +
            (u * v * bottomRight.X);
        var sourceY =
            ((1d - u) * (1d - v) * topLeft.Y) +
            (u * (1d - v) * topRight.Y) +
            ((1d - u) * v * bottomLeft.Y) +
            (u * v * bottomRight.Y);

        return new Point(
            cropLeftPx + (cropWidthPx * ClampUnit(sourceX)),
            cropTopPx + (cropHeightPx * ClampUnit(sourceY)));
    }
}
