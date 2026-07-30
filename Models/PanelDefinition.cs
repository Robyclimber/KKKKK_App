using System.Collections.ObjectModel;

namespace WallPanelPlanner.Models;

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

    public string? ImagePath { get; set; }

    public double ImageOffsetX { get; set; }

    public double ImageOffsetY { get; set; }

    public double ImageScale { get; set; } = 1d;

    public double ImageOpacity { get; set; } = 0.55d;

    public double ImageCropLeft { get; set; }

    public double ImageCropTop { get; set; }

    public double ImageCropRight { get; set; }

    public double ImageCropBottom { get; set; }

    public double EffectiveImageCropLeft => ClampCrop(ImageCropLeft);

    public double EffectiveImageCropTop => ClampCrop(ImageCropTop);

    public double EffectiveImageCropRight => ClampCrop(ImageCropRight);

    public double EffectiveImageCropBottom => ClampCrop(ImageCropBottom);

    public double EffectiveImageCropWidthFactor => Math.Max(0.001d, 1d - EffectiveImageCropLeft - EffectiveImageCropRight);

    public double EffectiveImageCropHeightFactor => Math.Max(0.001d, 1d - EffectiveImageCropTop - EffectiveImageCropBottom);

    public string Summary =>
        $"{Name} - Pos({X:0.#}, {Y:0.#}) mm - {Width:0.#} x {Height:0.#} mm - Fori: {HoleCount}";

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

        for (var columnIndex = 0; columnIndex < xColumns.Count; columnIndex++)
        {
            var ySequence = columnIndex % 2 == 0
                ? yRows
                : yRows.AsEnumerable().Reverse();

            foreach (var y in ySequence)
            {
                holes.Add(new HoleDefinition(holeNumber++, xColumns[columnIndex], y));
            }
        }

        return new ReadOnlyCollection<HoleDefinition>(holes);
    }

    private static double ClampCrop(double value)
    {
        return Math.Clamp(value, 0d, 0.999d);
    }
}
