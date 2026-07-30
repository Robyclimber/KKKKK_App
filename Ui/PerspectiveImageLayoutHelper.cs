using Microsoft.Maui.Controls.Shapes;
using RouteLab.Models;

namespace RouteLab.Ui;

public static class PerspectiveImageLayoutHelper
{
    public static void Render(
        AbsoluteLayout host,
        string imagePath,
        double opacity,
        Rect sourceImageRect,
        Rect selectionRect,
        PanelDefinition panel,
        int sliceCount = 28)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(panel);

        host.Children.Clear();
        if (string.IsNullOrWhiteSpace(imagePath) ||
            !File.Exists(imagePath) ||
            sourceImageRect.Width <= 1 ||
            sourceImageRect.Height <= 1 ||
            selectionRect.Width <= 1 ||
            selectionRect.Height <= 1)
        {
            return;
        }

        var topLeft = ToAbsolutePoint(selectionRect, panel.EffectivePerspectiveTopLeft);
        var topRight = ToAbsolutePoint(selectionRect, panel.EffectivePerspectiveTopRight);
        var bottomLeft = ToAbsolutePoint(selectionRect, panel.EffectivePerspectiveBottomLeft);
        var bottomRight = ToAbsolutePoint(selectionRect, panel.EffectivePerspectiveBottomRight);

        for (var index = 0; index < sliceCount; index++)
        {
            var v0 = (double)index / sliceCount;
            var v1 = (double)(index + 1) / sliceCount;
            var vm = (v0 + v1) / 2d;

            var destY = selectionRect.Top + (selectionRect.Height * v0);
            var destHeight = Math.Max(1d, (selectionRect.Height * (v1 - v0)) + 1d);

            var leftMid = Lerp(topLeft, bottomLeft, vm);
            var rightMid = Lerp(topRight, bottomRight, vm);
            var topMid = Lerp(Lerp(topLeft, topRight, 0.5d), Lerp(bottomLeft, bottomRight, 0.5d), v0);
            var bottomMid = Lerp(Lerp(topLeft, topRight, 0.5d), Lerp(bottomLeft, bottomRight, 0.5d), v1);

            var sourceWidth = Math.Max(2d, rightMid.X - leftMid.X);
            var sourceHeight = Math.Max(2d, bottomMid.Y - topMid.Y);
            var scaleX = selectionRect.Width / sourceWidth;
            var scaleY = destHeight / sourceHeight;

            var sliceHost = new Grid
            {
                Clip = new RectangleGeometry(new Rect(0d, 0d, selectionRect.Width, destHeight)),
                InputTransparent = true
            };

            var image = new Image
            {
                Source = ImageSource.FromFile(imagePath),
                Aspect = Aspect.Fill,
                Opacity = opacity,
                WidthRequest = sourceImageRect.Width * scaleX,
                HeightRequest = sourceImageRect.Height * scaleY,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                TranslationX = (sourceImageRect.Left - leftMid.X) * scaleX,
                TranslationY = (sourceImageRect.Top - topMid.Y) * scaleY,
                InputTransparent = true
            };

            sliceHost.Children.Add(image);
            AbsoluteLayout.SetLayoutBounds(sliceHost, new Rect(selectionRect.Left, destY, selectionRect.Width, destHeight));
            AbsoluteLayout.SetLayoutFlags(sliceHost, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
            host.Children.Add(sliceHost);
        }
    }

    private static Point ToAbsolutePoint(Rect rect, Point normalizedPoint)
    {
        return new Point(
            rect.Left + (normalizedPoint.X * rect.Width),
            rect.Top + (normalizedPoint.Y * rect.Height));
    }

    private static Point Lerp(Point start, Point end, double t)
    {
        return new Point(
            start.X + ((end.X - start.X) * t),
            start.Y + ((end.Y - start.Y) * t));
    }
}

