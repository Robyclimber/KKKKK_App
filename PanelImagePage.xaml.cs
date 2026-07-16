using System.Globalization;
using RuoteLab.Services;
using RuoteLab.ViewModels;

namespace RuoteLab;

public partial class PanelImagePage : ContentPage
{
    private readonly App app;
    private readonly GymSetupViewModel viewModel;
    private readonly IWallImageService wallImageService;
    private readonly IPanelImageAlignmentService panelImageAlignmentService;
    private bool isAdvancedVisible;

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
        Title = panel is null ? "Immagine pannello" : $"Immagine · {panel.Name}";
        PanelContextLabel.Text = panel is null
            ? "Nessun pannello selezionato."
            : $"Pannello selezionato: {panel.Name}";
        PanelImageInfoLabel.Text = panel is null
            ? "Seleziona un pannello dalla pagina parete."
            : string.IsNullOrWhiteSpace(panel.ImagePath)
                ? $"Nessuna immagine associata al pannello {panel.Name}."
                : $"Immagine del pannello {panel.Name}: {Path.GetFileName(panel.ImagePath)}";
        SuggestedFlowLabel.Text = panel is null
            ? "Seleziona un pannello dalla pagina parete."
            : string.IsNullOrWhiteSpace(panel.ImagePath)
                ? "Carica una foto del pannello per iniziare."
                : "Apri ritaglio, poi usa auto allineamento o rifinitura manuale.";

        var enabled = panel is not null;
        LoadImageButton.IsEnabled = enabled;
        ClearImageButton.IsEnabled = enabled;
        AnalyzeHoldsButton.IsEnabled = enabled;
        AutoAlignButton.IsEnabled = enabled;
        OpenCropEditorButton.IsEnabled = enabled;
        ApplyAlignmentButton.IsEnabled = enabled;
        ToggleAdvancedButton.IsEnabled = enabled;

        ImageOffsetXEntry.Text = ToEditorText(panel?.ImageOffsetX ?? 0d);
        ImageOffsetYEntry.Text = ToEditorText(panel?.ImageOffsetY ?? 0d);
        ImageScaleSlider.Value = panel is null || panel.ImageScale <= 0 ? 1d : panel.ImageScale;
        ImageOpacitySlider.Value = panel is null || panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity;
        ImageCropLeftEntry.Text = ToPercentEditorText(panel?.ImageCropLeft ?? 0d);
        ImageCropTopEntry.Text = ToPercentEditorText(panel?.ImageCropTop ?? 0d);
        ImageCropRightEntry.Text = ToPercentEditorText(panel?.ImageCropRight ?? 0d);
        ImageCropBottomEntry.Text = ToPercentEditorText(panel?.ImageCropBottom ?? 0d);

        ImagePreview.Source = panel is not null && !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)
            ? ImageSource.FromFile(panel.ImagePath)
            : null;
        ImagePreview.IsVisible = ImagePreview.Source is not null;
        ImagePreviewEmptyLabel.IsVisible = ImagePreview.Source is null;

        AdvancedSection.IsVisible = isAdvancedVisible;
        ToggleAdvancedButton.Text = isAdvancedVisible ? "Nascondi controlli avanzati" : "Mostra controlli avanzati";

        UpdateMeters();
    }

    private async void OnLoadImageClicked(object? sender, EventArgs e)
    {
        try
        {
            EnsurePanelSelected();
            var importedPath = await wallImageService.PickAndImportImageAsync();
            if (!string.IsNullOrWhiteSpace(importedPath))
            {
                viewModel.SetSelectedPanelImage(importedPath);
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

            await Navigation.PushAsync(new HoldAnalysisPage(viewModel, app.WallConfigurationStorageService, viewModel.SelectedWall));
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Immagine pannello", ex.Message, "OK");
        }
    }

    private async void OnAutoAlignClicked(object? sender, EventArgs e)
    {
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
}
