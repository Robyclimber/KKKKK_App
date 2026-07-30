using RuoteLab.Models;
using RuoteLab.ViewModels;
using Microsoft.Maui.Layouts;

namespace RuoteLab;

public partial class PanelCropEditorPage : ContentPage
{
    private enum CropDragMode
    {
        None,
        Move,
        Left,
        Top,
        Right,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private enum PerspectiveDragMode
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private readonly GymSetupViewModel viewModel;
    private Rect imageBounds;
    private CropDragMode dragMode;
    private double startLeft;
    private double startTop;
    private double startRight;
    private double startBottom;
    private PerspectiveDragMode perspectiveDragMode;
    private Point startPerspectiveTopLeft;
    private Point startPerspectiveTopRight;
    private Point startPerspectiveBottomLeft;
    private Point startPerspectiveBottomRight;

    public PanelCropEditorPage()
    {
        InitializeComponent();
        viewModel = ((App)Application.Current!).GymSetupViewModel;
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;
        RefreshView();
    }

    private void OnCropViewportSizeChanged(object? sender, EventArgs e)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath) || CropViewport.Width <= 0 || CropViewport.Height <= 0)
        {
            imageBounds = Rect.Zero;
            CropImage.IsVisible = false;
            SelectionBorder.IsVisible = false;
            HandleLeft.IsVisible = false;
            HandleTop.IsVisible = false;
            HandleRight.IsVisible = false;
            HandleBottom.IsVisible = false;
            HandleTopLeft.IsVisible = false;
            HandleTopRight.IsVisible = false;
            HandleBottomLeft.IsVisible = false;
            HandleBottomRight.IsVisible = false;
            MaskTop.IsVisible = false;
            MaskBottom.IsVisible = false;
            MaskLeft.IsVisible = false;
            MaskRight.IsVisible = false;
            EmptyLabel.IsVisible = true;
            CropInfoLabel.Text = "Seleziona un pannello con immagine";
            return;
        }

        CropInfoLabel.Text = $"Pannello {panel.Name}";
        CropImage.Source = ImageSource.FromFile(panel.ImagePath);
        CropImage.IsVisible = true;
        EmptyLabel.IsVisible = false;

        imageBounds = GetImageBounds(panel.ImagePath!, CropViewport.Width, CropViewport.Height);
        AbsoluteLayout.SetLayoutBounds(CropImage, imageBounds);
        AbsoluteLayout.SetLayoutFlags(CropImage, AbsoluteLayoutFlags.None);

        var selectionX = imageBounds.X + (panel.EffectiveImageCropLeft * imageBounds.Width);
        var selectionY = imageBounds.Y + (panel.EffectiveImageCropTop * imageBounds.Height);
        var selectionWidth = panel.EffectiveImageCropWidthFactor * imageBounds.Width;
        var selectionHeight = panel.EffectiveImageCropHeightFactor * imageBounds.Height;
        var selectionRect = new Rect(selectionX, selectionY, selectionWidth, selectionHeight);

        SelectionBorder.IsVisible = true;
        AbsoluteLayout.SetLayoutBounds(SelectionBorder, selectionRect);
        AbsoluteLayout.SetLayoutFlags(SelectionBorder, AbsoluteLayoutFlags.None);

        UpdateHandle(HandleLeft, selectionRect.Left, selectionRect.Center.Y);
        UpdateHandle(HandleTop, selectionRect.Center.X, selectionRect.Top);
        UpdateHandle(HandleRight, selectionRect.Right, selectionRect.Center.Y);
        UpdateHandle(HandleBottom, selectionRect.Center.X, selectionRect.Bottom);
        UpdateHandle(HandleTopLeft, selectionRect.Left, selectionRect.Top);
        UpdateHandle(HandleTopRight, selectionRect.Right, selectionRect.Top);
        UpdateHandle(HandleBottomLeft, selectionRect.Left, selectionRect.Bottom);
        UpdateHandle(HandleBottomRight, selectionRect.Right, selectionRect.Bottom);
        UpdatePerspectiveHandle(PerspectiveHandleTopLeft, selectionRect, panel.EffectivePerspectiveTopLeft);
        UpdatePerspectiveHandle(PerspectiveHandleTopRight, selectionRect, panel.EffectivePerspectiveTopRight);
        UpdatePerspectiveHandle(PerspectiveHandleBottomLeft, selectionRect, panel.EffectivePerspectiveBottomLeft);
        UpdatePerspectiveHandle(PerspectiveHandleBottomRight, selectionRect, panel.EffectivePerspectiveBottomRight);
        UpdateMasks(imageBounds, selectionRect);
    }

    private void OnMovePanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.Move, e);
    private void OnLeftPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.Left, e);
    private void OnTopPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.Top, e);
    private void OnRightPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.Right, e);
    private void OnBottomPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.Bottom, e);
    private void OnTopLeftPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.TopLeft, e);
    private void OnTopRightPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.TopRight, e);
    private void OnBottomLeftPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.BottomLeft, e);
    private void OnBottomRightPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(CropDragMode.BottomRight, e);
    private void OnPerspectiveTopLeftPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePerspectivePan(PerspectiveDragMode.TopLeft, e);
    private void OnPerspectiveTopRightPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePerspectivePan(PerspectiveDragMode.TopRight, e);
    private void OnPerspectiveBottomLeftPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePerspectivePan(PerspectiveDragMode.BottomLeft, e);
    private void OnPerspectiveBottomRightPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePerspectivePan(PerspectiveDragMode.BottomRight, e);

    private void HandlePan(CropDragMode mode, PanUpdatedEventArgs e)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null || imageBounds.Width <= 1 || imageBounds.Height <= 1)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                dragMode = mode;
                startLeft = panel.EffectiveImageCropLeft;
                startTop = panel.EffectiveImageCropTop;
                startRight = panel.EffectiveImageCropRight;
                startBottom = panel.EffectiveImageCropBottom;
                break;

            case GestureStatus.Running:
                ApplyDrag(e.TotalX / imageBounds.Width, e.TotalY / imageBounds.Height);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                dragMode = CropDragMode.None;
                break;
        }
    }

    private void HandlePerspectivePan(PerspectiveDragMode mode, PanUpdatedEventArgs e)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null || imageBounds.Width <= 1 || imageBounds.Height <= 1)
        {
            return;
        }

        var selectionRect = new Rect(
            imageBounds.X + (panel.EffectiveImageCropLeft * imageBounds.Width),
            imageBounds.Y + (panel.EffectiveImageCropTop * imageBounds.Height),
            panel.EffectiveImageCropWidthFactor * imageBounds.Width,
            panel.EffectiveImageCropHeightFactor * imageBounds.Height);

        if (selectionRect.Width <= 1 || selectionRect.Height <= 1)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                perspectiveDragMode = mode;
                startPerspectiveTopLeft = panel.EffectivePerspectiveTopLeft;
                startPerspectiveTopRight = panel.EffectivePerspectiveTopRight;
                startPerspectiveBottomLeft = panel.EffectivePerspectiveBottomLeft;
                startPerspectiveBottomRight = panel.EffectivePerspectiveBottomRight;
                break;

            case GestureStatus.Running:
                ApplyPerspectiveDrag(e.TotalX / selectionRect.Width, e.TotalY / selectionRect.Height);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                perspectiveDragMode = PerspectiveDragMode.None;
                break;
        }
    }

    private void ApplyDrag(double dxRatio, double dyRatio)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null)
        {
            return;
        }

        const double minFactor = 0.00001d;
        double left = startLeft;
        double top = startTop;
        double right = startRight;
        double bottom = startBottom;

        switch (dragMode)
        {
            case CropDragMode.Move:
            {
                var width = 1d - left - right;
                var height = 1d - top - bottom;
                left = Math.Clamp(startLeft + dxRatio, 0d, 1d - width);
                top = Math.Clamp(startTop + dyRatio, 0d, 1d - height);
                right = 1d - width - left;
                bottom = 1d - height - top;
                break;
            }
            case CropDragMode.Left:
                left = Math.Clamp(startLeft + dxRatio, 0d, 1d - right - minFactor);
                break;
            case CropDragMode.Top:
                top = Math.Clamp(startTop + dyRatio, 0d, 1d - bottom - minFactor);
                break;
            case CropDragMode.Right:
                right = Math.Clamp(startRight - dxRatio, 0d, 1d - left - minFactor);
                break;
            case CropDragMode.Bottom:
                bottom = Math.Clamp(startBottom - dyRatio, 0d, 1d - top - minFactor);
                break;
            case CropDragMode.TopLeft:
                left = Math.Clamp(startLeft + dxRatio, 0d, 1d - right - minFactor);
                top = Math.Clamp(startTop + dyRatio, 0d, 1d - bottom - minFactor);
                break;
            case CropDragMode.TopRight:
                right = Math.Clamp(startRight - dxRatio, 0d, 1d - left - minFactor);
                top = Math.Clamp(startTop + dyRatio, 0d, 1d - bottom - minFactor);
                break;
            case CropDragMode.BottomLeft:
                left = Math.Clamp(startLeft + dxRatio, 0d, 1d - right - minFactor);
                bottom = Math.Clamp(startBottom - dyRatio, 0d, 1d - top - minFactor);
                break;
            case CropDragMode.BottomRight:
                right = Math.Clamp(startRight - dxRatio, 0d, 1d - left - minFactor);
                bottom = Math.Clamp(startBottom - dyRatio, 0d, 1d - top - minFactor);
                break;
        }

        try
        {
            viewModel.UpdateSelectedPanelImageCrop(left, top, right, bottom);
            RefreshView();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ApplyPerspectiveDrag(double dxRatio, double dyRatio)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null)
        {
            return;
        }

        var topLeft = startPerspectiveTopLeft;
        var topRight = startPerspectiveTopRight;
        var bottomLeft = startPerspectiveBottomLeft;
        var bottomRight = startPerspectiveBottomRight;

        switch (perspectiveDragMode)
        {
            case PerspectiveDragMode.TopLeft:
                topLeft = new Point(ClampUnit(startPerspectiveTopLeft.X + dxRatio), ClampUnit(startPerspectiveTopLeft.Y + dyRatio));
                break;
            case PerspectiveDragMode.TopRight:
                topRight = new Point(ClampUnit(startPerspectiveTopRight.X + dxRatio), ClampUnit(startPerspectiveTopRight.Y + dyRatio));
                break;
            case PerspectiveDragMode.BottomLeft:
                bottomLeft = new Point(ClampUnit(startPerspectiveBottomLeft.X + dxRatio), ClampUnit(startPerspectiveBottomLeft.Y + dyRatio));
                break;
            case PerspectiveDragMode.BottomRight:
                bottomRight = new Point(ClampUnit(startPerspectiveBottomRight.X + dxRatio), ClampUnit(startPerspectiveBottomRight.Y + dyRatio));
                break;
        }

        viewModel.UpdateSelectedPanelImagePerspective(
            topLeft.X,
            topLeft.Y,
            topRight.X,
            topRight.Y,
            bottomLeft.X,
            bottomLeft.Y,
            bottomRight.X,
            bottomRight.Y);
        RefreshView();
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            return;
        }

        viewModel.UpdateSelectedPanelImageCrop(0d, 0d, 0d, 0d);
        RefreshView();
    }

    private void OnResetPerspectiveClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            return;
        }

        viewModel.UpdateSelectedPanelImagePerspective(0d, 0d, 1d, 0d, 0d, 1d, 1d, 1d);
        RefreshView();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private static void UpdateHandle(VisualElement handle, double centerX, double centerY)
    {
        handle.IsVisible = true;
        var width = handle.WidthRequest > 0 ? handle.WidthRequest : 28d;
        var height = handle.HeightRequest > 0 ? handle.HeightRequest : 28d;
        AbsoluteLayout.SetLayoutBounds(handle, new Rect(centerX - (width / 2d), centerY - (height / 2d), width, height));
        AbsoluteLayout.SetLayoutFlags(handle, AbsoluteLayoutFlags.None);
    }

    private static void UpdatePerspectiveHandle(VisualElement handle, Rect selectionRect, Point normalizedPoint)
    {
        var marginX = Math.Min(18d, selectionRect.Width * 0.12d);
        var marginY = Math.Min(18d, selectionRect.Height * 0.12d);
        var centerX = selectionRect.Left + marginX + (normalizedPoint.X * Math.Max(0d, selectionRect.Width - (marginX * 2d)));
        var centerY = selectionRect.Top + marginY + (normalizedPoint.Y * Math.Max(0d, selectionRect.Height - (marginY * 2d)));
        UpdateHandle(handle, centerX, centerY);
    }

    private void UpdateMasks(Rect imageRect, Rect selectionRect)
    {
        SetMask(MaskTop, new Rect(imageRect.X, imageRect.Y, imageRect.Width, Math.Max(0d, selectionRect.Y - imageRect.Y)));
        SetMask(MaskBottom, new Rect(imageRect.X, selectionRect.Bottom, imageRect.Width, Math.Max(0d, imageRect.Bottom - selectionRect.Bottom)));
        SetMask(MaskLeft, new Rect(imageRect.X, selectionRect.Y, Math.Max(0d, selectionRect.X - imageRect.X), selectionRect.Height));
        SetMask(MaskRight, new Rect(selectionRect.Right, selectionRect.Y, Math.Max(0d, imageRect.Right - selectionRect.Right), selectionRect.Height));
    }

    private static void SetMask(BoxView mask, Rect rect)
    {
        mask.IsVisible = rect.Width > 0.5d && rect.Height > 0.5d;
        AbsoluteLayout.SetLayoutBounds(mask, rect);
        AbsoluteLayout.SetLayoutFlags(mask, AbsoluteLayoutFlags.None);
    }

    private static Rect GetImageBounds(string imagePath, double viewportWidth, double viewportHeight)
    {
        var pixelSize = TryGetImagePixelSize(imagePath);
        if (pixelSize is null || pixelSize.Value.Width <= 0 || pixelSize.Value.Height <= 0)
        {
            return new Rect(0d, 0d, viewportWidth, viewportHeight);
        }

        var sourceAspect = pixelSize.Value.Width / pixelSize.Value.Height;
        var viewportAspect = viewportWidth / Math.Max(1d, viewportHeight);

        if (sourceAspect >= viewportAspect)
        {
            var width = viewportWidth;
            var height = width / sourceAspect;
            return new Rect(0d, (viewportHeight - height) / 2d, width, height);
        }

        var fittedHeight = viewportHeight;
        var fittedWidth = fittedHeight * sourceAspect;
        return new Rect((viewportWidth - fittedWidth) / 2d, 0d, fittedWidth, fittedHeight);
    }

    private static Size? TryGetImagePixelSize(string imagePath)
    {
#if ANDROID
        try
        {
            var options = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            Android.Graphics.BitmapFactory.DecodeFile(imagePath, options);
            if (options.OutWidth > 0 && options.OutHeight > 0)
            {
                return new Size(options.OutWidth, options.OutHeight);
            }
        }
        catch
        {
        }
#endif
        return null;
    }

    private static double ClampUnit(double value)
    {
        return Math.Clamp(value, 0d, 1d);
    }
}
