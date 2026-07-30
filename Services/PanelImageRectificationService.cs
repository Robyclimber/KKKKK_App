using RouteLab.Models;
using SkiaSharp;

namespace RouteLab.Services;

public sealed class PanelImageRectificationService : IPanelImageRectificationService
{
    private const int MinimumLongestSidePixels = 256;
    private const int MaximumOutputPixels = 1600;
    private const int MaximumSourceDimension = 1600;

    public Task<PanelImageRectificationResult> GenerateAsync(
        string sourceImagePath,
        IReadOnlyList<Point> sourceCorners,
        double panelWidth,
        double panelHeight,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
        {
            return Task.FromResult(PanelImageRectificationResult.Failure("Immagine sorgente non disponibile."));
        }

        if (sourceCorners.Count != 4 || panelWidth <= 0d || panelHeight <= 0d)
        {
            return Task.FromResult(PanelImageRectificationResult.Failure("Geometria del pannello non valida."));
        }

        return Task.Run(
            () => GenerateCore(sourceImagePath, sourceCorners, panelWidth, panelHeight, cancellationToken),
            cancellationToken);
    }

    private static PanelImageRectificationResult GenerateCore(
        string sourceImagePath,
        IReadOnlyList<Point> sourceCorners,
        double panelWidth,
        double panelHeight,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sourceBitmap = DecodeScaledBitmap(sourceImagePath, cancellationToken);
            if (sourceBitmap is null)
            {
                return PanelImageRectificationResult.Failure("Impossibile decodificare la foto del pannello.");
            }

            var sourcePoints = sourceCorners
                .Select(point => new SKPoint(
                    (float)(Math.Clamp(point.X, 0d, 1d) * (sourceBitmap.Width - 1)),
                    (float)(Math.Clamp(point.Y, 0d, 1d) * (sourceBitmap.Height - 1))))
                .ToArray();

            var (outputWidth, outputHeight) = CalculateOutputSize(sourcePoints, panelWidth, panelHeight);
            using var rectifiedBitmap = WarpPerspective(sourceBitmap, sourcePoints, outputWidth, outputHeight);
            cancellationToken.ThrowIfCancellationRequested();

            var outputFolder = Path.Combine(FileSystem.Current.AppDataDirectory, "panel-images", "rectified");
            Directory.CreateDirectory(outputFolder);
            var outputPath = Path.Combine(outputFolder, $"panel-{Guid.NewGuid():N}.png");
            SaveBitmap(rectifiedBitmap, outputPath);

            return new PanelImageRectificationResult(
                true,
                "Foto rettificata e adattata al formato del pannello.",
                outputPath,
                outputWidth,
                outputHeight);
        }
        catch (OperationCanceledException)
        {
            return PanelImageRectificationResult.Failure("Rettifica annullata.");
        }
        catch (Exception)
        {
            return PanelImageRectificationResult.Failure("Errore durante la rettifica della foto.");
        }
    }

    private static SKBitmap? DecodeScaledBitmap(string sourceImagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var codec = SKCodec.Create(sourceImagePath);
        if (codec is null)
        {
            return null;
        }

        var info = codec.Info;
        var sampleSize = 1;
        while ((info.Width / sampleSize) > MaximumSourceDimension ||
               (info.Height / sampleSize) > MaximumSourceDimension)
        {
            sampleSize *= 2;
        }

        var scaledInfo = sampleSize == 1
            ? info
            : new SKImageInfo(
                Math.Max(1, info.Width / sampleSize),
                Math.Max(1, info.Height / sampleSize),
                info.ColorType,
                info.AlphaType);

        var bitmap = new SKBitmap(scaledInfo);
        var decodeResult = codec.GetPixels(scaledInfo, bitmap.GetPixels());
        if (decodeResult is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }

    private static (int Width, int Height) CalculateOutputSize(
        IReadOnlyList<SKPoint> sourcePoints,
        double panelWidth,
        double panelHeight)
    {
        var measuredWidth = (Distance(sourcePoints[0], sourcePoints[1]) +
                             Distance(sourcePoints[3], sourcePoints[2])) / 2d;
        var measuredHeight = (Distance(sourcePoints[0], sourcePoints[3]) +
                              Distance(sourcePoints[1], sourcePoints[2])) / 2d;
        var measuredLongestSide = Math.Max(measuredWidth, measuredHeight);
        var longestSide = (int)Math.Round(Math.Clamp(
            measuredLongestSide,
            MinimumLongestSidePixels,
            MaximumOutputPixels));
        var panelAspect = panelWidth / panelHeight;

        return panelAspect >= 1d
            ? (longestSide, Math.Max(1, (int)Math.Round(longestSide / panelAspect)))
            : (Math.Max(1, (int)Math.Round(longestSide * panelAspect)), longestSide);
    }

    private static SKBitmap WarpPerspective(
        SKBitmap sourceBitmap,
        SKPoint[] sourcePoints,
        int outputWidth,
        int outputHeight)
    {
        var destinationPoints = new[]
        {
            new SKPoint(0f, 0f),
            new SKPoint(outputWidth - 1f, 0f),
            new SKPoint(outputWidth - 1f, outputHeight - 1f),
            new SKPoint(0f, outputHeight - 1f)
        };

        var inverseHomography = BuildHomography(destinationPoints, sourcePoints);
        var outputBitmap = new SKBitmap(outputWidth, outputHeight, sourceBitmap.ColorType, sourceBitmap.AlphaType);

        for (var y = 0; y < outputHeight; y++)
        {
            for (var x = 0; x < outputWidth; x++)
            {
                var source = TransformPoint(inverseHomography, x, y);
                outputBitmap.SetPixel(x, y, SampleBilinear(sourceBitmap, source.X, source.Y));
            }
        }

        return outputBitmap;
    }

    private static double Distance(SKPoint first, SKPoint second)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static SKColor SampleBilinear(SKBitmap bitmap, double x, double y)
    {
        var clampedX = Math.Clamp(x, 0d, bitmap.Width - 1d);
        var clampedY = Math.Clamp(y, 0d, bitmap.Height - 1d);
        var x0 = (int)Math.Floor(clampedX);
        var y0 = (int)Math.Floor(clampedY);
        var x1 = Math.Min(x0 + 1, bitmap.Width - 1);
        var y1 = Math.Min(y0 + 1, bitmap.Height - 1);
        var tx = clampedX - x0;
        var ty = clampedY - y0;
        var c00 = bitmap.GetPixel(x0, y0);
        var c10 = bitmap.GetPixel(x1, y0);
        var c01 = bitmap.GetPixel(x0, y1);
        var c11 = bitmap.GetPixel(x1, y1);

        return new SKColor(
            (byte)Interpolate(Interpolate(c00.Red, c10.Red, tx), Interpolate(c01.Red, c11.Red, tx), ty),
            (byte)Interpolate(Interpolate(c00.Green, c10.Green, tx), Interpolate(c01.Green, c11.Green, tx), ty),
            (byte)Interpolate(Interpolate(c00.Blue, c10.Blue, tx), Interpolate(c01.Blue, c11.Blue, tx), ty),
            (byte)Interpolate(Interpolate(c00.Alpha, c10.Alpha, tx), Interpolate(c01.Alpha, c11.Alpha, tx), ty));
    }

    private static double Interpolate(double start, double end, double factor) =>
        start + ((end - start) * factor);

    private static double[] BuildHomography(SKPoint[] source, SKPoint[] destination)
    {
        var matrix = new double[8, 9];
        for (var index = 0; index < 4; index++)
        {
            var sx = source[index].X;
            var sy = source[index].Y;
            var dx = destination[index].X;
            var dy = destination[index].Y;
            var row = index * 2;

            matrix[row, 0] = sx;
            matrix[row, 1] = sy;
            matrix[row, 2] = 1d;
            matrix[row, 6] = -sx * dx;
            matrix[row, 7] = -sy * dx;
            matrix[row, 8] = dx;
            matrix[row + 1, 3] = sx;
            matrix[row + 1, 4] = sy;
            matrix[row + 1, 5] = 1d;
            matrix[row + 1, 6] = -sx * dy;
            matrix[row + 1, 7] = -sy * dy;
            matrix[row + 1, 8] = dy;
        }

        return SolveLinearSystem(matrix);
    }

    private static double[] SolveLinearSystem(double[,] matrix)
    {
        const int rows = 8;
        const int columns = 9;
        for (var pivot = 0; pivot < rows; pivot++)
        {
            var maxRow = pivot;
            for (var row = pivot + 1; row < rows; row++)
            {
                if (Math.Abs(matrix[row, pivot]) > Math.Abs(matrix[maxRow, pivot]))
                {
                    maxRow = row;
                }
            }

            if (maxRow != pivot)
            {
                for (var column = 0; column < columns; column++)
                {
                    (matrix[pivot, column], matrix[maxRow, column]) =
                        (matrix[maxRow, column], matrix[pivot, column]);
                }
            }

            var pivotValue = matrix[pivot, pivot];
            if (Math.Abs(pivotValue) < 0.0000001d)
            {
                throw new InvalidOperationException("I quattro punti non formano un'area rettificabile.");
            }

            for (var column = pivot; column < columns; column++)
            {
                matrix[pivot, column] /= pivotValue;
            }

            for (var row = 0; row < rows; row++)
            {
                if (row == pivot)
                {
                    continue;
                }

                var factor = matrix[row, pivot];
                for (var column = pivot; column < columns; column++)
                {
                    matrix[row, column] -= factor * matrix[pivot, column];
                }
            }
        }

        return
        [
            matrix[0, 8], matrix[1, 8], matrix[2, 8],
            matrix[3, 8], matrix[4, 8], matrix[5, 8],
            matrix[6, 8], matrix[7, 8], 1d
        ];
    }

    private static (double X, double Y) TransformPoint(double[] homography, double x, double y)
    {
        var denominator = (homography[6] * x) + (homography[7] * y) + homography[8];
        if (Math.Abs(denominator) < 0.0000001d)
        {
            return (0d, 0d);
        }

        return (
            ((homography[0] * x) + (homography[1] * y) + homography[2]) / denominator,
            ((homography[3] * x) + (homography[4] * y) + homography[5]) / denominator);
    }

    private static void SaveBitmap(SKBitmap bitmap, string outputPath)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var outputStream = File.Create(outputPath);
        data.SaveTo(outputStream);
    }
}
