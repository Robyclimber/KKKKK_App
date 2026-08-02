using RouteLab.Drawing;
using RouteLab.Models;
using RouteLab.Services;
using RouteLab.ViewModels;

namespace RouteLab;

public partial class PanelCropEditorPage : ContentPage
{
    private const float MinimumScale = 1f;
    private const float MaximumScale = 8f;
    private const float ScaleStep = 0.5f;

    private enum PerspectiveDragMode
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private readonly GymSetupViewModel viewModel;
    private readonly IPanelImageRectificationService rectificationService;
    private readonly PanelImageEditorDrawable drawable;
    private readonly List<PanelImageEditorHandle> handles;
    private string? activeHandleId;
    private PointF interactionStart;
    private PointF panStart;
    private bool isCanvasPanning;
    private float scaleStart = 1f;
    private float offsetStartX;
    private float offsetStartY;
    private PanelImageCanvasTransformState transform = new();

    public PanelCropEditorPage()
    {
        InitializeComponent();
        var app = (App)Application.Current!;
        viewModel = app.GymSetupViewModel;
        rectificationService = app.PanelImageRectificationService;
        handles =
        [
            CreateHandle("TopLeft", 0.18f, 0.18f),
            CreateHandle("TopRight", 0.82f, 0.18f),
            CreateHandle("BottomRight", 0.82f, 0.82f),
            CreateHandle("BottomLeft", 0.18f, 0.82f)
        ];

        drawable = new PanelImageEditorDrawable
        {
            HandlesProvider = () => handles,
            TransformProvider = () => transform
        };
        EditorGraphicsView.Drawable = drawable;

        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        EditorHost.GestureRecognizers.Add(pinchGesture);

    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            LoadPanelState();
            ResetViewport();
        }
        catch (Exception ex)
        {
            _ = DisplayAlertAsync("Editor foto", $"Errore apertura editor: {ex.Message}", "OK");
        }
    }

    private void LoadPanelState()
    {
        var panel = viewModel.SelectedPanel;
        var sourceImagePath = GetSourceImagePath(panel);
        if (panel is null || sourceImagePath is null)
        {
            CropInfoLabel.Text = "Seleziona un pannello con immagine";
            drawable.ImagePath = null;
            EditorGraphicsView.Invalidate();
            return;
        }

        CropInfoLabel.Text = $"Pannello {panel.Name} - trascina i 4 punti come in HH";
        drawable.ImagePath = sourceImagePath;

        var left = (float)panel.EffectiveImageCropLeft;
        var top = (float)panel.EffectiveImageCropTop;
        var width = (float)panel.EffectiveImageCropWidthFactor;
        var height = (float)panel.EffectiveImageCropHeightFactor;

        handles[0].NormalizedPosition = ToAbsolutePoint(left, top, width, height, panel.EffectivePerspectiveTopLeft);
        handles[1].NormalizedPosition = ToAbsolutePoint(left, top, width, height, panel.EffectivePerspectiveTopRight);
        handles[2].NormalizedPosition = ToAbsolutePoint(left, top, width, height, panel.EffectivePerspectiveBottomRight);
        handles[3].NormalizedPosition = ToAbsolutePoint(left, top, width, height, panel.EffectivePerspectiveBottomLeft);
        EditorGraphicsView.Invalidate();
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (viewModel.SelectedPanel is null || e.Touches.Length != 1)
        {
            return;
        }

        var imageRect = drawable.LastImageRect;
        if (imageRect.Width <= 0 || imageRect.Height <= 0)
        {
            return;
        }

        interactionStart = e.Touches[0];
        panStart = new PointF(transform.OffsetX, transform.OffsetY);
        var canvasPoint = MapTouchToCanvas(interactionStart);
        activeHandleId = TryFindHandle(canvasPoint, imageRect);
        isCanvasPanning = activeHandleId is null && transform.Scale > MinimumScale;
        drawable.ActiveHandleId = activeHandleId;
        EditorGraphicsView.Invalidate();
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (viewModel.SelectedPanel is null || e.Touches.Length != 1)
        {
            return;
        }

        if (activeHandleId is not null)
        {
            MoveActiveHandleTo(e.Touches[0]);
        }
        else if (isCanvasPanning)
        {
            var current = e.Touches[0];
            UpdateTransform(
                transform.Scale,
                panStart.X + current.X - interactionStart.X,
                panStart.Y + current.Y - interactionStart.Y);
        }

        EditorGraphicsView.Invalidate();
    }

    private void OnEndInteraction(object? sender, TouchEventArgs e)
    {
        activeHandleId = null;
        isCanvasPanning = false;
        drawable.ActiveHandleId = null;
        EditorGraphicsView.Invalidate();
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            return;
        }

        switch (e.Status)
        {
            case GestureStatus.Started:
                scaleStart = transform.Scale;
                offsetStartX = transform.OffsetX;
                offsetStartY = transform.OffsetY;
                break;
            case GestureStatus.Running:
                var newScale = Math.Clamp(scaleStart * (float)e.Scale, MinimumScale, MaximumScale);
                var focusX = (float)(e.ScaleOrigin.X - 0.5d) * (float)EditorGraphicsView.Width;
                var focusY = (float)(e.ScaleOrigin.Y - 0.5d) * (float)EditorGraphicsView.Height;
                UpdateTransform(
                    newScale,
                    offsetStartX - (focusX * (newScale - scaleStart)),
                    offsetStartY - (focusY * (newScale - scaleStart)));
                EditorGraphicsView.Invalidate();
                break;
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        UpdateTransform(transform.Scale + ScaleStep, transform.OffsetX, transform.OffsetY);
        EditorGraphicsView.Invalidate();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        UpdateTransform(transform.Scale - ScaleStep, transform.OffsetX, transform.OffsetY);
        EditorGraphicsView.Invalidate();
    }

    private void OnResetViewportClicked(object? sender, EventArgs e)
    {
        ResetViewport();
    }

    private void OnResetGeometryClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            return;
        }

        handles[0].NormalizedPosition = new PointF(0f, 0f);
        handles[1].NormalizedPosition = new PointF(1f, 0f);
        handles[2].NormalizedPosition = new PointF(1f, 1f);
        handles[3].NormalizedPosition = new PointF(0f, 1f);
        EditorGraphicsView.Invalidate();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        var panel = viewModel.SelectedPanel;
        var sourceImagePath = GetSourceImagePath(panel);
        if (panel is null || sourceImagePath is null)
        {
            await DisplayAlertAsync("Editor foto", "Immagine sorgente non disponibile.", "OK");
            return;
        }

        using var busy = AppBusy.Show("Rettifica immagine...");
        ConfirmButton.IsEnabled = false;
        CropInfoLabel.Text = $"Generazione immagine per {panel.Name}...";

        try
        {
            var orderedCorners = GetOrderedHandlePoints();
            var result = await rectificationService.GenerateAsync(
                sourceImagePath,
                orderedCorners.Select(point => new Point(point.X, point.Y)).ToArray(),
                panel.Width,
                panel.Height);

            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.FilePath))
            {
                throw new InvalidOperationException(result.Message);
            }

            ApplyHandleGeometry(orderedCorners);
            viewModel.SetSelectedPanelRectifiedImage(sourceImagePath, result.FilePath);
            await viewModel.SaveSelectedWallAsync();

            await DisplayAlertAsync(
                "Immagine pannello",
                $"Immagine rettificata e salvata in formato {result.PixelWidth} x {result.PixelHeight} px.",
                "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            CropInfoLabel.Text = $"Pannello {panel.Name} - trascina i 4 punti come in HH";
            await DisplayAlertAsync("Editor foto", ex.Message, "OK");
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
        }
    }

    private void UpdateHandle(string id, PointF normalizedPosition)
    {
        var handle = handles.First(item => item.Id == id);
        handle.NormalizedPosition = normalizedPosition;
    }

    private void MoveActiveHandleTo(PointF touchPoint)
    {
        if (activeHandleId is null)
        {
            return;
        }

        var imageRect = drawable.LastImageRect;
        if (imageRect.Width <= 0 || imageRect.Height <= 0)
        {
            return;
        }

        var canvasPoint = MapTouchToCanvas(touchPoint);
        var normalized = new PointF(
            Math.Clamp((canvasPoint.X - imageRect.Left) / imageRect.Width, 0f, 1f),
            Math.Clamp((canvasPoint.Y - imageRect.Top) / imageRect.Height, 0f, 1f));

        UpdateHandle(activeHandleId, normalized);
    }

    private PointF[] GetOrderedHandlePoints()
    {
        var points = handles
            .Select(handle => new PointF(handle.NormalizedPosition.X, handle.NormalizedPosition.Y))
            .ToArray();

        return OrderCorners(points);
    }

    private void ApplyHandleGeometry(IReadOnlyList<PointF> ordered)
    {
        var minX = ordered.Min(point => point.X);
        var minY = ordered.Min(point => point.Y);
        var maxX = ordered.Max(point => point.X);
        var maxY = ordered.Max(point => point.Y);
        var width = Math.Max(0.001f, maxX - minX);
        var height = Math.Max(0.001f, maxY - minY);

        viewModel.UpdateSelectedPanelImageCrop(
            minX,
            minY,
            1f - maxX,
            1f - maxY);

        viewModel.UpdateSelectedPanelImagePerspective(
            (ordered[0].X - minX) / width,
            (ordered[0].Y - minY) / height,
            (ordered[1].X - minX) / width,
            (ordered[1].Y - minY) / height,
            (ordered[3].X - minX) / width,
            (ordered[3].Y - minY) / height,
            (ordered[2].X - minX) / width,
            (ordered[2].Y - minY) / height);
    }

    private static string? GetSourceImagePath(PanelDefinition? panel)
    {
        if (panel is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(panel.ImageSourcePath) && File.Exists(panel.ImageSourcePath))
        {
            return panel.ImageSourcePath;
        }

        return !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)
            ? panel.ImagePath
            : null;
    }

    private void UpdateTransform(float scale, float offsetX, float offsetY)
    {
        transform = new PanelImageCanvasTransformState
        {
            Scale = Math.Clamp(scale, MinimumScale, MaximumScale),
            OffsetX = offsetX,
            OffsetY = offsetY
        };
        ZoomLabel.Text = $"Zoom: {MathF.Round(transform.Scale * 100f)}%";
    }

    private void ResetViewport()
    {
        UpdateTransform(1f, 0f, 0f);
        activeHandleId = null;
        drawable.ActiveHandleId = null;
        EditorGraphicsView.Invalidate();
    }

    private PointF MapTouchToCanvas(PointF touch)
    {
        var scale = transform.Scale <= 0f ? 1f : transform.Scale;
        return new PointF(
            (touch.X - transform.OffsetX) / scale,
            (touch.Y - transform.OffsetY) / scale);
    }

    private string? TryFindHandle(PointF point, RectF imageRect)
    {
        const float radius = 28f;

        foreach (var handle in handles)
        {
            var handlePoint = new PointF(
                imageRect.Left + (handle.NormalizedPosition.X * imageRect.Width),
                imageRect.Top + (handle.NormalizedPosition.Y * imageRect.Height));

            var deltaX = handlePoint.X - point.X;
            var deltaY = handlePoint.Y - point.Y;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance <= radius)
            {
                return handle.Id;
            }
        }

        return null;
    }

    private static PanelImageEditorHandle CreateHandle(string id, float x, float y)
    {
        return new PanelImageEditorHandle
        {
            Id = id,
            NormalizedPosition = new PointF(x, y)
        };
    }

    private static PointF ToAbsolutePoint(float left, float top, float width, float height, Point perspectivePoint)
    {
        return new PointF(
            left + ((float)perspectivePoint.X * width),
            top + ((float)perspectivePoint.Y * height));
    }

    private static PointF[] OrderCorners(IReadOnlyList<PointF> points)
    {
        var orderedByY = points.OrderBy(point => point.Y).ToArray();
        var top = orderedByY.Take(2).OrderBy(point => point.X).ToArray();
        var bottom = orderedByY.Skip(2).OrderBy(point => point.X).ToArray();
        return
        [
            top[0],
            top[1],
            bottom[1],
            bottom[0]
        ];
    }
}
