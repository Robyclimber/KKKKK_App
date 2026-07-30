using RouteLab.Drawing;
using RouteLab.Models;
using RouteLab.ViewModels;

namespace RouteLab;

public partial class PanelHoleGridEditorPage : ContentPage
{
    private const float MinimumScale = 1f;
    private const float MaximumScale = 8f;
    private const float ScaleStep = 0.5f;

    private readonly GymSetupViewModel viewModel;
    private readonly PanelHoleGridDrawable drawable;
    private PanelImageCanvasTransformState transform = new();
    private PointF interactionStart;
    private PointF panStart;
    private bool isPanning;
    private bool isLoading;
    private float scaleStart = 1f;
    private float offsetStartX;
    private float offsetStartY;
    private double offsetX;
    private double offsetY;
    private double spacingX;
    private double spacingY;

    public PanelHoleGridEditorPage()
    {
        InitializeComponent();
        var app = (App)Application.Current!;
        viewModel = app.GymSetupViewModel;
        drawable = new PanelHoleGridDrawable
        {
            GridStateProvider = BuildGridState,
            TransformProvider = () => transform
        };
        EditorGraphicsView.Drawable = drawable;

        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        EditorHost.GestureRecognizers.Add(pinchGesture);

        var resetGesture = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        resetGesture.Tapped += OnEditorDoubleTapped;
        EditorHost.GestureRecognizers.Add(resetGesture);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadPanel();
        ResetViewport();
    }

    private void LoadPanel()
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null)
        {
            GridActions.CanSave = false;
            PanelInfoLabel.Text = "Nessun pannello selezionato";
            return;
        }

        isLoading = true;
        try
        {
            PanelInfoLabel.Text = $"{panel.Name} - {panel.Width:0.#} x {panel.Height:0.#} mm";
            OffsetXSlider.Maximum = Math.Max(1d, panel.Width / 2d);
            OffsetYSlider.Maximum = Math.Max(1d, panel.Height / 2d);
            SpacingXSlider.Maximum = Math.Max(1d, panel.Width);
            SpacingYSlider.Maximum = Math.Max(1d, panel.Height);
            OffsetXSlider.Value = Math.Clamp(panel.EdgeOffsetX, 0d, OffsetXSlider.Maximum);
            OffsetYSlider.Value = Math.Clamp(panel.EdgeOffsetY, 0d, OffsetYSlider.Maximum);
            SpacingXSlider.Value = Math.Clamp(panel.HorizontalSpacing, 1d, SpacingXSlider.Maximum);
            SpacingYSlider.Value = Math.Clamp(panel.VerticalSpacing, 1d, SpacingYSlider.Maximum);
            var hasRectifiedImage = panel.IsImageRectified &&
                                    !string.IsNullOrWhiteSpace(panel.ImagePath) &&
                                    File.Exists(panel.ImagePath);
            drawable.ImagePath = hasRectifiedImage ? panel.ImagePath : null;
            GeometryStatusLabel.Text = hasRectifiedImage
                ? "Griglia nello spazio rettificato del pannello"
                : !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)
                    ? "Esegui prima Ritaglia e adatta per allineare la griglia alla foto"
                    : "Nessuna foto: griglia sulle dimensioni del pannello";
            GridActions.CanSave = true;
            ReadSliderValues();
        }
        finally
        {
            isLoading = false;
        }

        RefreshLabelsAndPreview();
    }

    private void OnGridSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        ReadSliderValues();
        RefreshLabelsAndPreview();
    }

    private void ReadSliderValues()
    {
        offsetX = Math.Round(OffsetXSlider.Value, MidpointRounding.AwayFromZero);
        offsetY = Math.Round(OffsetYSlider.Value, MidpointRounding.AwayFromZero);
        spacingX = Math.Max(1d, Math.Round(SpacingXSlider.Value, MidpointRounding.AwayFromZero));
        spacingY = Math.Max(1d, Math.Round(SpacingYSlider.Value, MidpointRounding.AwayFromZero));
    }

    private void RefreshLabelsAndPreview()
    {
        OffsetXValueLabel.Text = $"Offset X: {offsetX:0} mm";
        OffsetYValueLabel.Text = $"Offset Y: {offsetY:0} mm";
        SpacingXValueLabel.Text = $"Passo X: {spacingX:0} mm";
        SpacingYValueLabel.Text = $"Passo Y: {spacingY:0} mm";

        var panel = viewModel.SelectedPanel;
        var xCount = panel is null ? 0 : CountCoordinates(offsetX, panel.Width, spacingX);
        var yCount = panel is null ? 0 : CountCoordinates(offsetY, panel.Height, spacingY);
        var holeCount = xCount * yCount;
        HoleCountLabel.Text = holeCount == 1 ? "1 foro" : $"{holeCount} fori";
        EditorGraphicsView.Invalidate();
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length != 1 || transform.Scale <= MinimumScale)
        {
            return;
        }

        interactionStart = e.Touches[0];
        panStart = new PointF(transform.OffsetX, transform.OffsetY);
        isPanning = true;
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (!isPanning || e.Touches.Length != 1)
        {
            return;
        }

        var current = e.Touches[0];
        UpdateTransform(
            transform.Scale,
            panStart.X + current.X - interactionStart.X,
            panStart.Y + current.Y - interactionStart.Y);
    }

    private void OnEndInteraction(object? sender, TouchEventArgs e)
    {
        isPanning = false;
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                scaleStart = transform.Scale;
                offsetStartX = transform.OffsetX;
                offsetStartY = transform.OffsetY;
                break;
            case GestureStatus.Running:
                var targetScale = Math.Clamp(scaleStart * (float)e.Scale, MinimumScale, MaximumScale);
                var focusX = (float)(e.ScaleOrigin.X * EditorGraphicsView.Width);
                var focusY = (float)(e.ScaleOrigin.Y * EditorGraphicsView.Height);
                var worldX = (focusX - offsetStartX) / scaleStart;
                var worldY = (focusY - offsetStartY) / scaleStart;
                UpdateTransform(
                    targetScale,
                    focusX - (worldX * targetScale),
                    focusY - (worldY * targetScale));
                break;
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        ZoomAroundCenter(transform.Scale + ScaleStep);
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        ZoomAroundCenter(transform.Scale - ScaleStep);
    }

    private void ZoomAroundCenter(float targetScale)
    {
        var centerX = (float)EditorGraphicsView.Width / 2f;
        var centerY = (float)EditorGraphicsView.Height / 2f;
        var currentScale = Math.Max(MinimumScale, transform.Scale);
        var worldX = (centerX - transform.OffsetX) / currentScale;
        var worldY = (centerY - transform.OffsetY) / currentScale;
        var clampedScale = Math.Clamp(targetScale, MinimumScale, MaximumScale);
        UpdateTransform(
            clampedScale,
            centerX - (worldX * clampedScale),
            centerY - (worldY * clampedScale));
    }

    private void OnResetViewportClicked(object? sender, EventArgs e)
    {
        ResetViewport();
    }

    private void OnEditorDoubleTapped(object? sender, TappedEventArgs e)
    {
        ResetViewport();
    }

    private void OnEditorSizeChanged(object? sender, EventArgs e)
    {
        UpdateTransform(transform.Scale, transform.OffsetX, transform.OffsetY);
    }

    private void ResetViewport()
    {
        UpdateTransform(MinimumScale, 0f, 0f);
    }

    private void UpdateTransform(float scale, float offsetXValue, float offsetYValue)
    {
        var clampedScale = Math.Clamp(scale, MinimumScale, MaximumScale);
        var viewportWidth = Math.Max(0f, (float)EditorGraphicsView.Width);
        var viewportHeight = Math.Max(0f, (float)EditorGraphicsView.Height);
        var minimumOffsetX = viewportWidth * (1f - clampedScale);
        var minimumOffsetY = viewportHeight * (1f - clampedScale);
        var clampedOffsetX = clampedScale <= MinimumScale
            ? 0f
            : Math.Clamp(offsetXValue, minimumOffsetX, 0f);
        var clampedOffsetY = clampedScale <= MinimumScale
            ? 0f
            : Math.Clamp(offsetYValue, minimumOffsetY, 0f);

        transform = new PanelImageCanvasTransformState
        {
            Scale = clampedScale,
            OffsetX = clampedOffsetX,
            OffsetY = clampedOffsetY
        };
        ZoomLabel.Text = $"Zoom: {MathF.Round(clampedScale * 100f)}% - trascina per muoverti";
        EditorGraphicsView.Invalidate();
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null)
        {
            await DisplayAlertAsync("Griglia fori", "Nessun pannello selezionato.", "OK");
            return;
        }

        using var busy = AppBusy.Show("Salvataggio griglia...");
        try
        {
            GridActions.CanSave = false;
            viewModel.UpdateSelectedPanel(new PanelInput
            {
                Name = panel.Name,
                X = panel.X,
                Y = panel.Y,
                Width = panel.Width,
                Height = panel.Height,
                EdgeOffsetX = offsetX,
                EdgeOffsetY = offsetY,
                HorizontalSpacing = spacingX,
                VerticalSpacing = spacingY,
                LedRoutingAxis = panel.LedRoutingAxis,
                LedStartDirection = panel.LedStartDirection
            });
            await viewModel.SaveSelectedWallAsync();
            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException ex)
        {
            GridActions.CanSave = true;
            await DisplayAlertAsync("Griglia fori", ex.Message, "OK");
        }
    }

    private PanelHoleGridState BuildGridState()
    {
        var panel = viewModel.SelectedPanel;
        return new PanelHoleGridState
        {
            PanelWidth = panel?.Width ?? 0d,
            PanelHeight = panel?.Height ?? 0d,
            OffsetX = offsetX,
            OffsetY = offsetY,
            SpacingX = spacingX,
            SpacingY = spacingY
        };
    }

    private static int CountCoordinates(double offset, double size, double spacing)
    {
        if (offset < 0d || size <= 0d || spacing <= 0d || offset > size / 2d)
        {
            return 0;
        }

        return Math.Max(0, (int)Math.Floor(((size - (offset * 2d)) / spacing) + 0.0001d) + 1);
    }
}
