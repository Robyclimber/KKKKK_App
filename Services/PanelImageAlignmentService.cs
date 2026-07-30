using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public sealed class PanelImageAlignmentService : IPanelImageAlignmentService
{
    public Task<PanelImageAlignmentSuggestion?> SuggestAlignmentAsync(PanelDefinition panel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
        {
            return Task.FromResult<PanelImageAlignmentSuggestion?>(null);
        }

#if ANDROID
        return Task.FromResult(TrySuggestOnAndroid(panel));
#else
        return Task.FromResult<PanelImageAlignmentSuggestion?>(null);
#endif
    }

#if ANDROID
    private static PanelImageAlignmentSuggestion? TrySuggestOnAndroid(PanelDefinition panel)
    {
        try
        {
            var holes = panel.GetOrderedHoles();
            if (holes.Count < 6)
            {
                return null;
            }

            using var bitmap = Android.Graphics.BitmapFactory.DecodeFile(panel.ImagePath!);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return null;
            }

            var sampledHoles = holes.Count <= 48
                ? holes
                : holes.Where((_, index) => index % 2 == 0).Take(48).ToList();

            var scaleCandidates = new[] { 0.88d, 0.94d, 1.0d, 1.06d, 1.12d };
            var offsetCandidates = new[] { -140d, -100d, -60d, -30d, 0d, 30d, 60d, 100d, 140d };

            var bestScore = double.MinValue;
            var bestScale = panel.ImageScale > 0 ? panel.ImageScale : 1d;
            var bestOffsetX = panel.ImageOffsetX;
            var bestOffsetY = panel.ImageOffsetY;

            foreach (var scale in scaleCandidates)
            {
                foreach (var offsetX in offsetCandidates)
                {
                    foreach (var offsetY in offsetCandidates)
                    {
                        var score = ScoreAlignment(bitmap, panel, sampledHoles, offsetX, offsetY, scale);
                        if (score <= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestScale = scale;
                        bestOffsetX = offsetX;
                        bestOffsetY = offsetY;
                    }
                }
            }

            var fineOffsets = new[] { -24d, -12d, 0d, 12d, 24d };
            var fineScales = new[] { -0.04d, -0.02d, 0d, 0.02d, 0.04d };

            foreach (var scaleDelta in fineScales)
            {
                var candidateScale = Math.Clamp(bestScale + scaleDelta, 0.2d, 2.5d);
                foreach (var offsetXDelta in fineOffsets)
                {
                    foreach (var offsetYDelta in fineOffsets)
                    {
                        var candidateOffsetX = bestOffsetX + offsetXDelta;
                        var candidateOffsetY = bestOffsetY + offsetYDelta;
                        var score = ScoreAlignment(bitmap, panel, sampledHoles, candidateOffsetX, candidateOffsetY, candidateScale);
                        if (score <= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestScale = candidateScale;
                        bestOffsetX = candidateOffsetX;
                        bestOffsetY = candidateOffsetY;
                    }
                }
            }

            if (double.IsNegativeInfinity(bestScore) || double.IsNaN(bestScore))
            {
                return null;
            }

            var confidence = Math.Clamp((bestScore + 1.2d) / 2.4d, 0.05d, 0.98d);
            return new PanelImageAlignmentSuggestion
            {
                OffsetX = Math.Round(bestOffsetX, 1, MidpointRounding.AwayFromZero),
                OffsetY = Math.Round(bestOffsetY, 1, MidpointRounding.AwayFromZero),
                Scale = Math.Round(bestScale, 2, MidpointRounding.AwayFromZero),
                Confidence = confidence,
                Reason = $"Auto-align euristico sui fori campionati ({sampledHoles.Count})"
            };
        }
        catch
        {
            return null;
        }
    }

    private static double ScoreAlignment(
        Android.Graphics.Bitmap bitmap,
        PanelDefinition panel,
        IReadOnlyList<HoleDefinition> holes,
        double offsetX,
        double offsetY,
        double scale)
    {
        var sum = 0d;
        var count = 0;

        foreach (var hole in holes)
        {
            var cropWidthFactor = panel.EffectiveImageCropWidthFactor;
            var cropHeightFactor = panel.EffectiveImageCropHeightFactor;
            var xRatio = panel.EffectiveImageCropLeft + (((hole.X - offsetX) / Math.Max(1d, panel.Width * scale)) * cropWidthFactor);
            var yRatio = panel.EffectiveImageCropTop + (((hole.Y - offsetY) / Math.Max(1d, panel.Height * scale)) * cropHeightFactor);
            if (xRatio <= 0.03d || xRatio >= 0.97d || yRatio <= 0.03d || yRatio >= 0.97d)
            {
                continue;
            }

            var centerX = xRatio * bitmap.Width;
            var centerY = yRatio * bitmap.Height;
            var localScore = SampleHoleScore(bitmap, centerX, centerY);
            sum += localScore;
            count++;
        }

        if (count == 0)
        {
            return double.NegativeInfinity;
        }

        return sum / count;
    }

    private static double SampleHoleScore(Android.Graphics.Bitmap bitmap, double centerX, double centerY)
    {
        var cx = (int)Math.Round(centerX);
        var cy = (int)Math.Round(centerY);
        const int innerRadius = 6;
        const int outerRadius = 12;

        var innerSum = 0d;
        var innerCount = 0;
        var outerSum = 0d;
        var outerCount = 0;

        for (var dy = -outerRadius; dy <= outerRadius; dy++)
        {
            for (var dx = -outerRadius; dx <= outerRadius; dx++)
            {
                var x = cx + dx;
                var y = cy + dy;
                if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
                {
                    continue;
                }

                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared > outerRadius * outerRadius)
                {
                    continue;
                }

                var color = new Android.Graphics.Color(bitmap.GetPixel(x, y));
                var luminance = ((0.2126d * color.R) + (0.7152d * color.G) + (0.0722d * color.B)) / 255d;

                if (distanceSquared <= innerRadius * innerRadius)
                {
                    innerSum += luminance;
                    innerCount++;
                }
                else
                {
                    outerSum += luminance;
                    outerCount++;
                }
            }
        }

        if (innerCount == 0 || outerCount == 0)
        {
            return -1d;
        }

        var innerMean = innerSum / innerCount;
        var outerMean = outerSum / outerCount;

        return (outerMean - innerMean) + ((1d - innerMean) * 0.35d);
    }
#endif
}
