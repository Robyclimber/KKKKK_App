using System.Globalization;
using Shapes = Microsoft.Maui.Controls.Shapes;
using RouteLab.Models;
using RouteLab.Services;
using RouteLab.Ui;
using RouteLab.ViewModels;

namespace RouteLab;

public partial class PanelImagePage : ContentPage
{
    private const double MaximumPreviewHeight = 420d;

    private readonly App app;
    private readonly GymSetupViewModel viewModel;
    private readonly IWallImageService wallImageService;
    private readonly IPanelImageAlignmentService panelImageAlignmentService;
    private bool isAdvancedVisible;
    private bool isUpdatingPreviewTransform;

    public PanelImagePage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
        viewModel = app.GymSetupViewModel;
        wallImageService = app.WallImageService;
        panelImageAlignmentService = app.PanelImageAlignmentService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SyncView();
    }

    private void SyncView()
    {
        var panel = viewModel.SelectedPanel;
        Title = panel is null ? "Immagine pannello" : $"Immagine - {panel.Name}";
        PanelContextLabel.Text = panel is null
            ? "Nessun pannello selezionato."
            : $"Pannello selezionato: {panel.Name}";
        PanelFormatLabel.Text = BuildPanelFormatText(panel);
        PanelImageInfoLabel.Text = panel is null
            ? "Seleziona un pannello dalla pagina parete."
            : string.IsNullOrWhiteSpace(panel.ImagePath)
                ? $"Nessuna immagine associata al pannello {panel.Name}."
                : BuildImageInfoText(panel);
        SuggestedFlowLabel.Text = panel is null
            ? "Seleziona un pannello dalla pagina parete."
            : string.IsNullOrWhiteSpace(panel.ImagePath)
                ? "Carica una foto del pannello per iniziare."
                : panel.IsImageRectified
                    ? "La foto rettificata e' l'immagine attiva del pannello."
                    : "Apri l'editor, definisci i quattro punti e conferma.";
        SuggestedStepsLabel.Text = panel is null
            ? "1. Seleziona pannello. 2. Carica foto. 3. Adatta e correggi."
            : string.IsNullOrWhiteSpace(panel.ImagePath)
                ? "1. Carica foto. 2. Apri editor."
                : panel.IsImageRectified
                    ? "Per cambiare il ritaglio riapri l'editor a quattro punti."
                    : "1. Apri editor. 2. Posiziona i punti. 3. Conferma e usa.";

        var hasPanel = panel is not null;
        var hasImage = hasPanel &&
            !string.IsNullOrWhiteSpace(panel!.ImagePath) &&
            File.Exists(panel.ImagePath);
        LoadImageButton.IsEnabled = hasPanel;
        ClearImageButton.IsEnabled = hasImage;
        AnalyzeHoldsButton.IsEnabled = hasImage;
        AutoAlignButton.IsEnabled = hasImage;
        OpenCropEditorButton.IsEnabled = hasImage;
        FitImageButton.IsEnabled = hasImage;
        ApplyAlignmentButton.IsEnabled = hasImage;
        ToggleAdvancedButton.IsEnabled = hasImage;
        AdvancedSection.IsEnabled = hasImage;

        ImageOffsetXEntry.Text = ToEditorText(panel?.ImageOffsetX ?? 0d);
        ImageOffsetYEntry.Text = ToEditorText(panel?.ImageOffsetY ?? 0d);
        ImageScaleSlider.Value = panel is null || panel.ImageScale <= 0 ? 1d : panel.ImageScale;
        ImageOpacitySlider.Value = panel is null || panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity;
        ImageCropLeftEntry.Text = ToPercentEditorText(panel?.ImageCropLeft ?? 0d);
        ImageCropTopEntry.Text = ToPercentEditorText(panel?.ImageCropTop ?? 0d);
        ImageCropRightEntry.Text = ToPercentEditorText(panel?.ImageCropRight ?? 0d);
        ImageCropBottomEntry.Text = ToPercentEditorText(panel?.ImageCropBottom ?? 0d);

        ImagePreviewWarpHost.IsVisible = hasImage;
        ImagePreviewEmptyLabel.IsVisible = !hasImage;

        AdvancedSection.IsVisible = isAdvancedVisible;
        ToggleAdvancedButton.Text = isAdvancedVisible ? "Nascondi controlli avanzati" : "Mostra controlli avanzati";

        UpdateMeters();
        UpdatePreviewViewportSize(panel);
        ApplyImagePreviewTransform();
    }

    private void OnImagePreviewFrameSizeChanged(object? sender, EventArgs e)
    {
        UpdatePreviewViewportSize(viewModel.SelectedPanel);
    }

    private void OnImagePreviewHostSizeChanged(object? sender, EventArgs e)
    {
        ApplyImagePreviewTransform();
    }

    private void UpdatePreviewViewportSize(PanelDefinition? panel)
    {
        var availableWidth = ImagePreviewFrame.Width - ImagePreviewFrame.Padding.HorizontalThickness;
        if (availableWidth <= 1d)
        {
            return;
        }

        var panelAspect = panel is not null && panel.Width > 0d && panel.Height > 0d
            ? panel.Width / panel.Height
            : 1d;
        var previewWidth = availableWidth;
        var previewHeight = previewWidth / panelAspect;

        if (previewHeight > MaximumPreviewHeight)
        {
            previewHeight = MaximumPreviewHeight;
            previewWidth = previewHeight * panelAspect;
        }

        previewWidth = Math.Max(1d, previewWidth);
        previewHeight = Math.Max(1d, previewHeight);
        if (Math.Abs(ImagePreviewHost.WidthRequest - previewWidth) < 0.5d &&
            Math.Abs(ImagePreviewHost.HeightRequest - previewHeight) < 0.5d)
        {
            return;
        }

        ImagePreviewHost.WidthRequest = previewWidth;
        ImagePreviewHost.HeightRequest = previewHeight;
    }

    private async void OnLoadImageClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Caricamento immagine...");
        try
        {
            EnsurePanelSelected();
            var importedPath = await wallImageService.PickAndImportImageAsync();
            if (!string.IsNullOrWhiteSpace(importedPath))
            {
                viewModel.SetSelectedPanelImage(importedPath);
                var panel = viewModel.SelectedPanel!;
                var imageSize = ImageFileSizeReader.TryGetPixelSize(importedPath);
                if (imageSize is not null && imageSize.Value.Width > 0 && imageSize.Value.Height > 0)
                {
                    FitImageCropToPanel(panel, imageSize.Value);
                }
            }

            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private async void OnClearImageClicked(object? sender, EventArgs e)
    {
        try
        {
            EnsurePanelSelected();
            viewModel.ClearSelectedPanelImage();
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private async void OnFitImageClicked(object? sender, EventArgs e)
    {
        try
        {
            EnsurePanelSelected();
            var panel = viewModel.SelectedPanel!;
            if (string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
            {
                throw new InvalidOperationException("Carica prima una foto del pannello.");
            }

            var imageSize = ImageFileSizeReader.TryGetPixelSize(panel.ImagePath);
            if (imageSize is null || imageSize.Value.Width <= 0 || imageSize.Value.Height <= 0)
            {
                throw new InvalidOperationException("Non riesco a leggere il formato della foto per adattarla al pannello.");
            }

            FitImageCropToPanel(panel, imageSize.Value);
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private async void OnAnalyzeHoldsClicked(object? sender, EventArgs e)
    {
        try
        {
            EnsurePanelSelected();
            if (viewModel.SelectedWall is null)
            {
                throw new InvalidOperationException("Seleziona prima una parete.");
            }

            if (string.IsNullOrWhiteSpace(viewModel.SelectedPanel!.ImagePath) || !File.Exists(viewModel.SelectedPanel.ImagePath))
            {
                throw new InvalidOperationException("Carica prima una foto del pannello.");
            }

            await Navigation.PushAsync(new HoldAnalysisPage(
                app.WallConfigurationStorageService,
                viewModel.SelectedWall,
                viewModel.SelectedPanel!));
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private async void OnAutoAlignClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Analisi immagine...");
        try
        {
            EnsurePanelSelected();
            if (string.IsNullOrWhiteSpace(viewModel.SelectedPanel!.ImagePath) || !File.Exists(viewModel.SelectedPanel.ImagePath))
            {
                throw new InvalidOperationException("Carica prima una foto del pannello.");
            }

            var suggestion = await panelImageAlignmentService.SuggestAlignmentAsync(viewModel.SelectedPanel);
            if (suggestion is null)
            {
                throw new InvalidOperationException("Auto allineamento non disponibile su questa immagine o su questa piattaforma.");
            }

            viewModel.UpdateSelectedPanelImageAlignment(
                suggestion.OffsetX,
                suggestion.OffsetY,
                suggestion.Scale,
                ImageOpacitySlider.Value);

            SyncView();
            await DisplayAlertAsync(
                "Auto allineamento",
                $"Offset X: {suggestion.OffsetX:0.#} mm\nOffset Y: {suggestion.OffsetY:0.#} mm\nScala: {suggestion.Scale:0.00}",
                "OK");
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private async void OnApplyAlignmentClicked(object? sender, EventArgs e)
    {
        try
        {
            EnsurePanelSelected();
            viewModel.UpdateSelectedPanelImageCrop(
                ParsePercent(ImageCropLeftEntry.Text),
                ParsePercent(ImageCropTopEntry.Text),
                ParsePercent(ImageCropRightEntry.Text),
                ParsePercent(ImageCropBottomEntry.Text));
            viewModel.UpdateSelectedPanelImageAlignment(
                ParseDouble(ImageOffsetXEntry.Text),
                ParseDouble(ImageOffsetYEntry.Text),
                ImageScaleSlider.Value,
                ImageOpacitySlider.Value);
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private void OnScaleCoarseDecreaseClicked(object? sender, EventArgs e) => AdjustScale(-0.10d);
    private void OnScaleFineDecreaseClicked(object? sender, EventArgs e) => AdjustScale(-0.01d);
    private void OnScaleFineIncreaseClicked(object? sender, EventArgs e) => AdjustScale(0.01d);
    private void OnScaleCoarseIncreaseClicked(object? sender, EventArgs e) => AdjustScale(0.10d);

    private void OnResetCropClicked(object? sender, EventArgs e)
    {
        EnsurePanelSelected();
        viewModel.UpdateSelectedPanelImageCrop(0d, 0d, 0d, 0d);
        SyncView();
    }

    private void OnToggleAdvancedClicked(object? sender, EventArgs e)
    {
        isAdvancedVisible = !isAdvancedVisible;
        AdvancedSection.IsVisible = isAdvancedVisible;
        ToggleAdvancedButton.Text = isAdvancedVisible ? "Nascondi controlli avanzati" : "Mostra controlli avanzati";
    }

    private async void OnOpenCropEditorClicked(object? sender, EventArgs e)
    {
        try
        {
            EnsurePanelSelected();
            if (string.IsNullOrWhiteSpace(viewModel.SelectedPanel!.ImagePath) || !File.Exists(viewModel.SelectedPanel.ImagePath))
            {
                throw new InvalidOperationException("Carica prima una foto del pannello.");
            }

            await Navigation.PushAsync(new PanelCropEditorPage());
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private void AdjustScale(double delta)
    {
        var nextValue = Math.Clamp(ImageScaleSlider.Value + delta, ImageScaleSlider.Minimum, ImageScaleSlider.Maximum);
        ImageScaleSlider.Value = Math.Round(nextValue, 2, MidpointRounding.AwayFromZero);
        UpdateMeters();
    }

    private void UpdateMeters()
    {
        ImageScaleValueLabel.Text = $"Scala attuale: {ImageScaleSlider.Value:0.00}";
        ImageOpacityValueLabel.Text = $"Opacita attuale: {Math.Round(ImageOpacitySlider.Value * 100d):0}%";
    }

    private void ApplyImagePreviewTransform()
    {
        if (isUpdatingPreviewTransform)
        {
            return;
        }

        isUpdatingPreviewTransform = true;
        try
        {
            var panel = viewModel.SelectedPanel;
            if (panel is null ||
                string.IsNullOrWhiteSpace(panel.ImagePath) ||
                !File.Exists(panel.ImagePath) ||
                ImagePreviewHost.Width <= 1 ||
                ImagePreviewHost.Height <= 1)
            {
                ImagePreviewWarpHost.Children.Clear();
                ImagePreviewHost.Clip = null;
                return;
            }

            if (panel.IsImageRectified)
            {
                PerspectiveImageLayoutHelper.Render(
                    ImagePreviewWarpHost,
                    panel.ImagePath!,
                    panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity,
                    new Rect(0d, 0d, ImagePreviewHost.Width, ImagePreviewHost.Height),
                    new Rect(0d, 0d, ImagePreviewHost.Width, ImagePreviewHost.Height),
                    panel);
                ImagePreviewHost.Clip = new Shapes.RectangleGeometry(
                    new Rect(0d, 0d, ImagePreviewHost.Width, ImagePreviewHost.Height));
                return;
            }

            var imageBounds = GetImageBounds(panel.ImagePath, ImagePreviewHost.Width, ImagePreviewHost.Height);
            var cropWidthFactor = panel.EffectiveImageCropWidthFactor;
            var cropHeightFactor = panel.EffectiveImageCropHeightFactor;
            var previewWidth = imageBounds.Width / cropWidthFactor;
            var previewHeight = imageBounds.Height / cropHeightFactor;
            var previewX = imageBounds.X - (panel.EffectiveImageCropLeft * previewWidth);
            var previewY = imageBounds.Y - (panel.EffectiveImageCropTop * previewHeight);

            PerspectiveImageLayoutHelper.Render(
                ImagePreviewWarpHost,
                panel.ImagePath!,
                panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity,
                new Rect(previewX, previewY, previewWidth, previewHeight),
                new Rect(0d, 0d, ImagePreviewHost.Width, ImagePreviewHost.Height),
                panel);
            ImagePreviewHost.Clip = new Shapes.RectangleGeometry(new Rect(0d, 0d, ImagePreviewHost.Width, ImagePreviewHost.Height));
        }
        finally
        {
            isUpdatingPreviewTransform = false;
        }
    }

    private static Rect GetImageBounds(string imagePath, double viewportWidth, double viewportHeight)
    {
        var pixelSize = ImageFileSizeReader.TryGetPixelSize(imagePath);
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

    private void EnsurePanelSelected()
    {
        if (viewModel.SelectedPanel is null)
        {
            throw new InvalidOperationException("Seleziona prima un pannello.");
        }
    }

    private static string ToEditorText(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string ToPercentEditorText(double value) => (value * 100d).ToString("0.##", CultureInfo.InvariantCulture);

    private static double ParseDouble(string? text)
    {
        var normalized = text?.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidOperationException("Controlla i valori numerici inseriti.");
    }

    private static double ParsePercent(string? text)
    {
        var percent = ParseDouble(text);
        var value = percent / 100d;
        if (value < 0d || value > 99.9d / 100d)
        {
            throw new InvalidOperationException("Il ritaglio deve stare tra 0 e 99.9 percento.");
        }

        return value;
    }

    private void FitImageCropToPanel(PanelDefinition panel, Size imageSize)
    {
        var panelAspect = panel.Width / Math.Max(1d, panel.Height);
        var imageAspect = imageSize.Width / Math.Max(1d, imageSize.Height);

        if (panelAspect <= 0 || imageAspect <= 0)
        {
            throw new InvalidOperationException("Formato pannello o immagine non valido.");
        }

        if (Math.Abs(panelAspect - imageAspect) < 0.0001d)
        {
            viewModel.UpdateSelectedPanelImageCrop(0d, 0d, 0d, 0d);
            return;
        }

        if (imageAspect > panelAspect)
        {
            var keptWidthRatio = panelAspect / imageAspect;
            var cropHorizontal = (1d - keptWidthRatio) / 2d;
            viewModel.UpdateSelectedPanelImageCrop(cropHorizontal, 0d, cropHorizontal, 0d);
            return;
        }

        var keptHeightRatio = imageAspect / panelAspect;
        var cropVertical = (1d - keptHeightRatio) / 2d;
        viewModel.UpdateSelectedPanelImageCrop(0d, cropVertical, 0d, cropVertical);
    }

    private static string BuildPanelFormatText(PanelDefinition? panel)
    {
        if (panel is null)
        {
            return "Formato pannello non disponibile.";
        }

        return $"Pannello: {panel.Width:0.#} x {panel.Height:0.#} mm - {GetOrientationLabel(panel.Width, panel.Height)}";
    }

    private static string BuildImageInfoText(PanelDefinition panel)
    {
        var fileName = Path.GetFileName(panel.ImagePath);
        var imageSize = ImageFileSizeReader.TryGetPixelSize(panel.ImagePath!);
        var status = panel.IsImageRectified ? "Rettificata" : "Originale";
        if (imageSize is null)
        {
            return $"{status} - {fileName}";
        }

        return $"{status} - {fileName} - {imageSize.Value.Width:0} x {imageSize.Value.Height:0} px - {GetOrientationLabel(imageSize.Value.Width, imageSize.Value.Height)}";
    }

    private static string GetOrientationLabel(double width, double height)
    {
        if (Math.Abs(width - height) < 0.01d)
        {
            return "quadrato";
        }

        return width > height ? "orizzontale" : "verticale";
    }
}
