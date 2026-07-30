using RuoteLab.Models;

namespace RuoteLab.Services;

public sealed class HoldAnalysisSuggestionService : IHoldAnalysisSuggestionService
{
    public HoldSuggestion Suggest(WallDefinition wall, WallHoleDefinition hole)
    {
        ArgumentNullException.ThrowIfNull(wall);

        var imageSuggestion = TrySuggestFromImage(wall, hole);
        if (imageSuggestion is not null)
        {
            return imageSuggestion;
        }

        return SuggestFromPosition(wall, hole);
    }

    private static HoldSuggestion SuggestFromPosition(WallDefinition wall, WallHoleDefinition hole)
    {
        var xRatio = wall.Width <= 0 ? 0.5d : hole.AbsoluteX / wall.Width;
        var yRatio = wall.Height <= 0 ? 0.5d : hole.AbsoluteY / wall.Height;

        if (yRatio >= 0.78d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = yRatio >= 0.9d ? HoldSize.Xs : HoldSize.S,
                HoldType = HoldType.Foothold,
                Reason = "Foro molto basso: proposta piedi."
            };
        }

        if (yRatio <= 0.18d && xRatio >= 0.18d && xRatio <= 0.82d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.L,
                HoldType = HoldType.Jug,
                Reason = "Foro alto in zona centrale: proposta maniglia grande."
            };
        }

        if (xRatio <= 0.12d || xRatio >= 0.88d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.M,
                HoldType = HoldType.Pinch,
                Reason = "Foro vicino al bordo: proposta pinza media."
            };
        }

        if (yRatio <= 0.35d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.M,
                HoldType = HoldType.Edge,
                Reason = "Foro alto/intermedio: proposta tacca media."
            };
        }

        if (yRatio <= 0.6d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.M,
                HoldType = HoldType.Sloper,
                Reason = "Foro centrale: proposta svasa media."
            };
        }

        return new HoldSuggestion
        {
            HasHold = true,
            HoldSize = HoldSize.L,
            HoldType = HoldType.Jug,
            Reason = "Foro basso/intermedio: proposta maniglia grande."
        };
    }

    private static HoldSuggestion? TrySuggestFromImage(WallDefinition wall, WallHoleDefinition hole)
    {
        var panel = wall.FindPanel(hole);
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
        {
            return null;
        }

#if ANDROID
        try
        {
            var cropArea = TryGetHoleCropArea(wall, hole);
            if (cropArea is null)
            {
                return null;
            }

            using var bitmap = Android.Graphics.BitmapFactory.DecodeFile(panel.ImagePath);
            if (bitmap is null)
            {
                return null;
            }

            var crop = cropArea.Value;
            var cropWidth = Math.Min(crop.Size, bitmap.Width);
            var cropHeight = Math.Min(crop.Size, bitmap.Height);
            var cropLeft = Math.Clamp(crop.Left, 0, Math.Max(0, bitmap.Width - cropWidth));
            var cropTop = Math.Clamp(crop.Top, 0, Math.Max(0, bitmap.Height - cropHeight));

            using var croppedBitmap = Android.Graphics.Bitmap.CreateBitmap(bitmap, cropLeft, cropTop, cropWidth, cropHeight);
            return AnalyzeHoleCrop(wall, hole, croppedBitmap);
        }
        catch
        {
            return null;
        }
#else
        return null;
#endif
    }

#if ANDROID
    private static HoldSuggestion AnalyzeHoleCrop(WallDefinition wall, WallHoleDefinition hole, Android.Graphics.Bitmap croppedBitmap)
    {
        var width = croppedBitmap.Width;
        var height = croppedBitmap.Height;
        if (width <= 0 || height <= 0)
        {
            return SuggestFromPosition(wall, hole);
        }

        var pixels = new int[width * height];
        croppedBitmap.GetPixels(pixels, 0, width, 0, 0, width, height);

        var totalLuma = 0d;
        var totalLumaSquared = 0d;
        var darkPixels = 0;
        var veryDarkPixels = 0;
        var brightPixels = 0;
        var centerDarkPixels = 0;
        var centerPixels = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = new Android.Graphics.Color(pixels[(y * width) + x]);
                var luma = ((0.2126d * color.R) + (0.7152d * color.G) + (0.0722d * color.B)) / 255d;
                totalLuma += luma;
                totalLumaSquared += luma * luma;

                if (luma < 0.45d)
                {
                    darkPixels++;
                }

                if (luma < 0.28d)
                {
                    veryDarkPixels++;
                }

                if (luma > 0.7d)
                {
                    brightPixels++;
                }

                var insideCenterX = x >= width * 0.28d && x <= width * 0.72d;
                var insideCenterY = y >= height * 0.28d && y <= height * 0.72d;
                if (!insideCenterX || !insideCenterY)
                {
                    continue;
                }

                centerPixels++;
                if (luma < 0.42d)
                {
                    centerDarkPixels++;
                }
            }
        }

        var pixelCount = Math.Max(1, pixels.Length);
        var meanLuma = totalLuma / pixelCount;
        var variance = Math.Max(0d, (totalLumaSquared / pixelCount) - (meanLuma * meanLuma));
        var contrast = Math.Sqrt(variance);
        var darkRatio = darkPixels / (double)pixelCount;
        var veryDarkRatio = veryDarkPixels / (double)pixelCount;
        var brightRatio = brightPixels / (double)pixelCount;
        var centerDarkRatio = centerPixels == 0 ? 0d : centerDarkPixels / (double)centerPixels;

        var xRatio = wall.Width <= 0 ? 0.5d : hole.AbsoluteX / wall.Width;
        var yRatio = wall.Height <= 0 ? 0.5d : hole.AbsoluteY / wall.Height;

        if (yRatio >= 0.8d && centerDarkRatio < 0.22d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = darkRatio > 0.3d ? HoldSize.S : HoldSize.Xs,
                HoldType = HoldType.Foothold,
                Reason = "Crop basso con dettaglio contenuto: proposta piedi."
            };
        }

        if (centerDarkRatio >= 0.52d && contrast >= 0.2d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = darkRatio >= 0.42d ? HoldSize.Xl : HoldSize.L,
                HoldType = HoldType.Jug,
                Reason = "Crop con massa centrale marcata: proposta maniglia."
            };
        }

        if (contrast >= 0.22d && darkRatio <= 0.26d && brightRatio >= 0.18d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.M,
                HoldType = HoldType.Edge,
                Reason = "Crop con contrasto netto e spigoli leggibili: proposta tacca."
            };
        }

        if (meanLuma >= 0.58d && contrast <= 0.16d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.M,
                HoldType = HoldType.Sloper,
                Reason = "Crop chiaro e uniforme: proposta svasa."
            };
        }

        if ((xRatio <= 0.12d || xRatio >= 0.88d) && veryDarkRatio >= 0.18d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.M,
                HoldType = HoldType.Pinch,
                Reason = "Crop laterale con volume concentrato: proposta pinza."
            };
        }

        if (centerDarkRatio >= 0.35d && contrast >= 0.14d && darkRatio < 0.34d)
        {
            return new HoldSuggestion
            {
                HasHold = true,
                HoldSize = HoldSize.S,
                HoldType = HoldType.Pocket,
                Reason = "Crop con cavita centrale compatta: proposta bidito."
            };
        }

        var fallbackSuggestion = SuggestFromPosition(wall, hole);
        return new HoldSuggestion
        {
            HasHold = fallbackSuggestion.HasHold,
            HoldSize = fallbackSuggestion.HoldSize,
            HoldType = fallbackSuggestion.HoldType,
            Reason = $"{fallbackSuggestion.Reason} (fallback dopo analisi crop non conclusiva)"
        };
    }

    private static HoleCropArea? TryGetHoleCropArea(WallDefinition wall, WallHoleDefinition hole)
    {
        var panel = wall.FindPanel(hole);
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath))
        {
            return null;
        }

        var pixelSize = TryGetImagePixelSize(panel.ImagePath);
        if (pixelSize is null)
        {
            return null;
        }

        var sourceWidth = Math.Max(1d, pixelSize.Value.Width);
        var sourceHeight = Math.Max(1d, pixelSize.Value.Height);
        var imageScale = Math.Max(0.2d, panel.ImageScale);
        var overlayWidth = Math.Max(1d, panel.Width * imageScale);
        var overlayHeight = Math.Max(1d, panel.Height * imageScale);
        var holeOverlayX = hole.RelativeX - panel.ImageOffsetX;
        var holeOverlayY = hole.RelativeY - panel.ImageOffsetY;
        var sourcePoint = panel.MapPanelPointToImageSource(holeOverlayX / overlayWidth, holeOverlayY / overlayHeight, sourceWidth, sourceHeight);
        var sourceHoleX = sourcePoint.X;
        var sourceHoleY = sourcePoint.Y;
        var cropWidthPx = sourceWidth * panel.EffectiveImageCropWidthFactor;
        var cropHeightPx = sourceHeight * panel.EffectiveImageCropHeightFactor;

        const double cropWindowMillimeters = 220d;
        var cropScaleX = cropWidthPx / overlayWidth;
        var cropScaleY = cropHeightPx / overlayHeight;
        var cropSizePx = (int)Math.Round(cropWindowMillimeters * ((cropScaleX + cropScaleY) / 2d));
        cropSizePx = Math.Max(96, cropSizePx);

        var cropLeft = (int)Math.Round(sourceHoleX - (cropSizePx / 2d));
        var cropTop = (int)Math.Round(sourceHoleY - (cropSizePx / 2d));
        return new HoleCropArea(cropLeft, cropTop, cropSizePx);
    }

    private static (int Width, int Height)? TryGetImagePixelSize(string imagePath)
    {
        try
        {
            var options = new Android.Graphics.BitmapFactory.Options
            {
                InJustDecodeBounds = true
            };

            Android.Graphics.BitmapFactory.DecodeFile(imagePath, options);
            if (options.OutWidth > 0 && options.OutHeight > 0)
            {
                return (options.OutWidth, options.OutHeight);
            }
        }
        catch
        {
        }

        return null;
    }

    private readonly record struct HoleCropArea(int Left, int Top, int Size);
#endif
}
