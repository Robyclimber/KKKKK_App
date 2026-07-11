using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using WallPanelPlanner.Drawing;
using WallPanelPlanner.Models;
using WallPanelPlanner.ViewModels;

namespace WallPanelPlanner;

public partial class GymSetupPage : ContentPage
{
    private enum CropDragMode
    {
        None,
        Move,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private readonly Services.IGymSetupEditorStateService editorStateService;
    private readonly Services.IGymSetupPageStateService pageStateService;
    private readonly GymSetupViewModel viewModel;
    private readonly LayoutPreviewDrawable previewDrawable;
    private readonly Services.IWallImageService wallImageService;
    private readonly Services.IPanelImageAlignmentService panelImageAlignmentService;
    private double previewZoom = 1d;
    private double previewZoomStart = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;
    private bool isSyncingSelection;
    private Rect cropEditorImageBounds;
    private CropDragMode cropDragMode;
    private double cropDragStartLeft;
    private double cropDragStartTop;
    private double cropDragStartRight;
    private double cropDragStartBottom;

    public GymSetupPage()
    {
        try
        {
            InitializeComponent();

            var app = (App)Application.Current!;
            editorStateService = app.GymSetupEditorStateService;
            pageStateService = app.GymSetupPageStateService;
            viewModel = app.GymSetupViewModel;
            previewDrawable = app.LayoutPreviewDrawable;
            wallImageService = app.WallImageService;
            panelImageAlignmentService = app.PanelImageAlignmentService;

            RoomsPicker.ItemsSource = viewModel.Rooms;
            PreviewCanvas.Drawable = previewDrawable;

            ApplyWallEditorDefaults();
            ApplyPanelEditorState(resetToDefaults: true);
            Loaded += OnPageLoaded;
        }
        catch (Exception ex)
        {
            var databaseFactory = new Persistence.SqliteDatabaseFactory();
            var wallRepository = new Services.SqliteWallRepository(databaseFactory);
            var roomRepository = new Services.SqliteRoomRepository(databaseFactory);
            editorStateService = new Services.GymSetupEditorStateService();
            pageStateService = new Services.GymSetupPageStateService(editorStateService);
            viewModel = new ViewModels.GymSetupViewModel(
                new Services.GymSetupService(),
                new Services.WallConfigurationStorageService(wallRepository),
                wallRepository,
                roomRepository);
            previewDrawable = new LayoutPreviewDrawable();
            wallImageService = new Services.WallImageService();
            panelImageAlignmentService = new Services.PanelImageAlignmentService();
            Title = "Errore Configurazione";
            Content = BuildErrorView("Errore inizializzazione GymSetupPage", ex);
        }
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;
        await InitializeAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (Content is not null)
        {
            SyncViewFromState();
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await viewModel.LoadWallsAsync();
            ApplyWallEditorState(useSelectedWallValues: viewModel.SelectedWall is not null);
            ApplyPanelEditorState(resetToDefaults: true);
            SyncViewFromState();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Configura palestra", $"Errore inizializzazione Configura palestra: {ex.Message}", "OK");
        }
    }

    private async void OnAddWallClicked(object? sender, EventArgs e)
    {
        try
        {
            viewModel.AddWall(new WallInput
            {
                Name = WallNameEntry.Text?.Trim() ?? string.Empty,
                Width = ParsePositiveDouble(WallWidthEntry.Text, "Inserisci larghezza e altezza valide per la parete."),
                Height = ParsePositiveDouble(WallHeightEntry.Text, "Inserisci larghezza e altezza valide per la parete.")
            });

            ApplyWallEditorDefaults();
            ApplyPanelEditorState(resetToDefaults: true);
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnAddRoomClicked(object? sender, EventArgs e)
    {
        try
        {
            await viewModel.AddRoomAsync(RoomNameEntry.Text);
            ApplyWallEditorDefaults();
            ApplyPanelEditorState(resetToDefaults: true);
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnUpdateWallClicked(object? sender, EventArgs e)
    {
        try
        {
            viewModel.UpdateSelectedWall(new WallInput
            {
                Name = WallNameEntry.Text?.Trim() ?? string.Empty,
                Width = ParsePositiveDouble(WallWidthEntry.Text, "Inserisci larghezza e altezza valide per la parete."),
                Height = ParsePositiveDouble(WallHeightEntry.Text, "Inserisci larghezza e altezza valide per la parete.")
            });

            ApplyWallEditorState(useSelectedWallValues: true);
            ApplyPanelEditorState(resetToDefaults: true);
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnAddPanelClicked(object? sender, EventArgs e)
    {
        try
        {
            viewModel.AddPanel(ReadPanelInput());
            ApplyPanelEditorState(resetToDefaults: true);
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnUpdatePanelClicked(object? sender, EventArgs e)
    {
        try
        {
            viewModel.UpdateSelectedPanel(ReadPanelInput());
            ApplyPanelEditorState();
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnDeletePanelClicked(object? sender, EventArgs e)
    {
        try
        {
            viewModel.DeleteSelectedPanel();
            ApplyPanelEditorState(resetToDefaults: true);
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnSaveWallClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await viewModel.SaveSelectedWallAsync();
            await DisplayAlertAsync("Salvataggio completato", $"Parete salvata su database.\n{result}", "OK");
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnLoadWallImageClicked(object? sender, EventArgs e)
    {
        try
        {
            if (viewModel.SelectedPanel is null)
            {
                throw new InvalidOperationException("Seleziona prima un pannello.");
            }

            var importedPath = await wallImageService.PickAndImportImageAsync();
            if (string.IsNullOrWhiteSpace(importedPath))
            {
                return;
            }

            viewModel.SetSelectedPanelImage(importedPath);
            ApplyWallImageEditorState();
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnClearWallImageClicked(object? sender, EventArgs e)
    {
        try
        {
            viewModel.ClearSelectedPanelImage();
            ApplyWallImageEditorState();
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnAnalyzeHoldsClicked(object? sender, EventArgs e)
    {
        try
        {
            if (viewModel.SelectedWall is null)
            {
                throw new InvalidOperationException("Seleziona prima una parete.");
            }

            if (viewModel.SelectedPanel is null)
            {
                throw new InvalidOperationException("Seleziona prima un pannello.");
            }

            if (string.IsNullOrWhiteSpace(viewModel.SelectedPanel.ImagePath) || !File.Exists(viewModel.SelectedPanel.ImagePath))
            {
                throw new InvalidOperationException("Carica prima una foto del pannello.");
            }

            await Navigation.PushAsync(new HoldAnalysisPage(viewModel, ((App)Application.Current!).WallConfigurationStorageService, viewModel.SelectedWall));
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnAutoAlignPanelImageClicked(object? sender, EventArgs e)
    {
        try
        {
            if (viewModel.SelectedPanel is null)
            {
                throw new InvalidOperationException("Seleziona prima un pannello.");
            }

            if (string.IsNullOrWhiteSpace(viewModel.SelectedPanel.ImagePath) || !File.Exists(viewModel.SelectedPanel.ImagePath))
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
                WallImageOpacitySlider.Value);

            ApplyWallImageEditorState();
            SyncViewFromState();
            await DisplayAlertAsync(
                "Auto allineamento",
                $"Applicato.\nOffset X: {suggestion.OffsetX:0.#} mm\nOffset Y: {suggestion.OffsetY:0.#} mm\nScala: {suggestion.Scale:0.00}\nConfidenza: {suggestion.Confidence:P0}\n{suggestion.Reason}",
                "OK");
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnApplyWallImageAlignmentClicked(object? sender, EventArgs e)
    {
        try
        {
            viewModel.UpdateSelectedPanelImageCrop(
                ParsePercent(WallImageCropLeftEntry.Text),
                ParsePercent(WallImageCropTopEntry.Text),
                ParsePercent(WallImageCropRightEntry.Text),
                ParsePercent(WallImageCropBottomEntry.Text));

            viewModel.UpdateSelectedPanelImageAlignment(
                ParseDouble(WallImageOffsetXEntry.Text),
                ParseDouble(WallImageOffsetYEntry.Text),
                WallImageScaleSlider.Value,
                WallImageOpacitySlider.Value);

            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private void OnWallSelectionChanged(object? sender, EventArgs e)
    {
        if (isSyncingSelection)
        {
            return;
        }

        viewModel.SelectWall(WallsPicker.SelectedItem as WallDefinition);
        ApplyWallEditorState(useSelectedWallValues: viewModel.SelectedWall is not null);
        ApplyPanelEditorState(resetToDefaults: true);
        SyncViewFromState();
    }

    private void OnRoomSelectionChanged(object? sender, EventArgs e)
    {
        if (isSyncingSelection)
        {
            return;
        }

        viewModel.SelectRoom(RoomsPicker.SelectedItem as RoomDefinition);
        ApplyWallEditorDefaults();
        ApplyPanelEditorState(resetToDefaults: true);
        SyncViewFromState();
    }

    private void OnPreviewViewportSizeChanged(object? sender, EventArgs e)
    {
        if (PreviewViewport.Width <= 0 || PreviewViewport.Height <= 0)
        {
            return;
        }

        basePreviewWidth = Math.Max(280d, PreviewViewport.Width - 4d);
        basePreviewHeight = Math.Max(280d, PreviewViewport.Height - 4d);
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
    }

    private void OnCropEditorViewportSizeChanged(object? sender, EventArgs e)
    {
        UpdateCropEditorOverlay();
    }

    private void OnPreviewPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                previewZoomStart = previewZoom;
                break;
            case GestureStatus.Running:
                previewZoom = Math.Clamp(previewZoomStart * e.Scale, 1d, 4d);
                UpdatePreviewZoomLayout();
                break;
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom + 0.25d, 1d, 4d);
        previewZoomStart = previewZoom;
        UpdatePreviewZoomLayout();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom - 0.25d, 1d, 4d);
        previewZoomStart = previewZoom;
        UpdatePreviewZoomLayout();
    }

    private void OnZoomResetClicked(object? sender, EventArgs e)
    {
        previewZoom = 1d;
        previewZoomStart = 1d;
        UpdatePreviewZoomLayout();
    }

    private void OnPreviewDoubleTapped(object? sender, TappedEventArgs e)
    {
        previewZoom = 1d;
        previewZoomStart = 1d;
        UpdatePreviewZoomLayout();
    }

    private void OnWallImageScaleCoarseDecreaseClicked(object? sender, EventArgs e)
    {
        AdjustWallImageScale(-0.10d);
    }

    private void OnWallImageScaleFineDecreaseClicked(object? sender, EventArgs e)
    {
        AdjustWallImageScale(-0.01d);
    }

    private void OnWallImageScaleFineIncreaseClicked(object? sender, EventArgs e)
    {
        AdjustWallImageScale(0.01d);
    }

    private void OnWallImageScaleCoarseIncreaseClicked(object? sender, EventArgs e)
    {
        AdjustWallImageScale(0.10d);
    }

    private void OnWallImageScaleSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        UpdateWallImageScaleValueLabel();
        TryApplyLiveWallImageAlignment();
    }

    private void OnWallImageOpacitySliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        TryApplyLiveWallImageAlignment();
    }

    private void SyncViewFromState()
    {
        isSyncingSelection = true;
        var pageState = pageStateService.Build(viewModel);
        RoomsPicker.SelectedItem = pageState.SelectedRoom;
        WallsPicker.ItemsSource = pageState.VisibleWalls.ToList();
        WallsPicker.SelectedItem = pageState.SelectedWall;
        isSyncingSelection = false;

        WallInfoLabel.Text = pageState.WallInfoText;
        WorkflowTitleLabel.Text = pageState.WorkflowTitleText;
        WorkflowMessageLabel.Text = pageState.WorkflowMessageText;
        RoomSummaryLabel.Text = pageState.RoomSummaryText;
        WallSelectionHintLabel.Text = pageState.WallSelectionHintText;
        PanelEditorModeLabel.Text = pageState.PanelEditorModeText;
        PanelsEmptyLabel.IsVisible = pageState.ShowEmptyPanels;
        WallImageInfoLabel.Text = pageState.WallImageInfoText;
        AddWallButton.IsEnabled = pageState.CanAddWall;
        UpdateWallButton.IsEnabled = viewModel.HasSelectedWall;
        PanelsSectionBorder.IsEnabled = pageState.CanEditPanels;
        WallImageSectionBorder.IsEnabled = pageState.CanManageWallImage;
        LoadWallImageButton.IsEnabled = pageState.CanManageWallImage;
        ClearWallImageButton.IsEnabled = pageState.CanManageWallImage;
        AnalyzeHoldsButton.IsEnabled = pageState.CanManageWallImage;
        AutoAlignPanelImageButton.IsEnabled = pageState.CanManageWallImage;
        SaveWallButton.IsEnabled = pageState.CanSaveWall;
        OpenHardwareMappingButton.IsEnabled = viewModel.HasSelectedWall;

        previewDrawable.Wall = viewModel.SelectedWall;
        previewDrawable.SelectedPanel = viewModel.SelectedPanel;

        RebuildPanelsList();
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
        UpdateEditorButtons();
        ApplyWallImageEditorState();
    }

    private void ApplyWallEditorDefaults()
    {
        var editorState = editorStateService.BuildWallEditor(viewModel, useSelectedWallValues: false);
        RoomNameEntry.Text = editorState.RoomNameText;
        WallNameEntry.Text = editorState.WallNameText;
        WallWidthEntry.Text = editorState.WallWidthText;
        WallHeightEntry.Text = editorState.WallHeightText;
        WallEditorModeLabel.Text = editorState.ModeText;
    }

    private void ApplyWallEditorState(bool useSelectedWallValues)
    {
        var editorState = editorStateService.BuildWallEditor(viewModel, useSelectedWallValues);
        RoomNameEntry.Text = editorState.RoomNameText;
        WallNameEntry.Text = editorState.WallNameText;
        WallWidthEntry.Text = editorState.WallWidthText;
        WallHeightEntry.Text = editorState.WallHeightText;
        WallEditorModeLabel.Text = editorState.ModeText;
    }

    private void ApplyPanelEditorState(bool resetToDefaults = false)
    {
        var editorState = editorStateService.BuildPanelEditor(viewModel, useSelectedPanelValues: !resetToDefaults);
        PanelNameEntry.Text = editorState.PanelNameText;
        PanelXEntry.Text = editorState.PanelXText;
        PanelYEntry.Text = editorState.PanelYText;
        PanelWidthEntry.Text = editorState.PanelWidthText;
        PanelHeightEntry.Text = editorState.PanelHeightText;
        HoleOffsetEntry.Text = editorState.HoleOffsetText;
        HoleOffsetYEntry.Text = editorState.HoleOffsetYText;
        HoleHorizontalEntry.Text = editorState.HoleHorizontalText;
        HoleVerticalEntry.Text = editorState.HoleVerticalText;
        PanelEditorModeLabel.Text = editorState.ModeText;
    }

    private void UpdatePreviewZoomLayout()
    {
        previewDrawable.ZoomFactor = (float)previewZoom;
        var desiredSize = previewDrawable.GetDesiredSize(previewZoom);
        PreviewCanvas.WidthRequest = Math.Max(basePreviewWidth, desiredSize.Width);
        PreviewCanvas.HeightRequest = Math.Max(basePreviewHeight, desiredSize.Height);
        PreviewLayer.WidthRequest = PreviewCanvas.WidthRequest;
        PreviewLayer.HeightRequest = PreviewCanvas.HeightRequest;
        UpdateWallImageOverlay();
        PreviewCanvas.Invalidate();
    }

    private void UpdatePreviewBaseScale()
    {
        var wall = viewModel.SelectedWall;
        if (wall is null || wall.Width <= 0 || wall.Height <= 0)
        {
            previewDrawable.PixelsPerMillimeter = 0.1f;
            return;
        }

        const double padding = 48d;
        var availableWidth = Math.Max(1d, basePreviewWidth - padding);
        var availableHeight = Math.Max(1d, basePreviewHeight - padding);
        var fitScale = Math.Min(availableWidth / wall.Width, availableHeight / wall.Height);
        previewDrawable.PixelsPerMillimeter = (float)Math.Max(0.01d, fitScale);
    }

    private void UpdateEditorButtons()
    {
        AddPanelButton.IsEnabled = true;
        UpdatePanelButton.IsEnabled = viewModel.HasSelectedPanel;
        DeletePanelButton.IsEnabled = viewModel.HasSelectedPanel;
    }

    private void RebuildPanelsList()
    {
        PanelsHost.Children.Clear();
        var wall = viewModel.SelectedWall;
        if (wall is null)
        {
            return;
        }

        foreach (var panel in wall.Panels)
        {
            var isSelected = viewModel.IsPanelSelected(panel);
            var border = new Border
            {
                Background = isSelected ? Color.FromArgb("#2A2212") : Color.FromArgb("#191611"),
                Stroke = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#B9922F"),
                StrokeThickness = isSelected ? 3 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 0 },
                Padding = 12
            };

            border.Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = panel.Name,
                        FontSize = 18,
                        TextColor = Color.FromArgb("#F8E7A8")
                    },
                    new Label
                    {
                        Text = panel.Summary,
                        TextColor = Color.FromArgb("#D8A72D")
                    },
                    new Label
                    {
                        Text = isSelected ? "Pannello selezionato per modifica" : "Tocca per selezionare",
                        FontSize = 12,
                        TextColor = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#B9AA79")
                    }
                }
            };

            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    viewModel.SelectPanel(panel);
                    ApplyPanelEditorState();
                    SyncViewFromState();
                })
            });

            PanelsHost.Children.Add(border);
        }
    }

    private async void OnOpenHardwareMappingClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new HardwareMappingPage());
    }

    private async void OnOpenCropEditorClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            await ShowError("Seleziona prima un pannello.");
            return;
        }

        if (string.IsNullOrWhiteSpace(viewModel.SelectedPanel.ImagePath) || !File.Exists(viewModel.SelectedPanel.ImagePath))
        {
            await ShowError("Carica prima una foto del pannello.");
            return;
        }

        await Navigation.PushAsync(new PanelCropEditorPage());
    }

    private void ApplyWallImageEditorState()
    {
        var pageState = pageStateService.Build(viewModel);
        WallImageOffsetXEntry.Text = pageState.WallImageOffsetXText;
        WallImageOffsetYEntry.Text = pageState.WallImageOffsetYText;
        WallImageScaleSlider.Value = pageState.WallImageScale;
        WallImageOpacitySlider.Value = pageState.WallImageOpacity;
        WallImageCropLeftEntry.Text = pageState.WallImageCropLeftText;
        WallImageCropTopEntry.Text = pageState.WallImageCropTopText;
        WallImageCropRightEntry.Text = pageState.WallImageCropRightText;
        WallImageCropBottomEntry.Text = pageState.WallImageCropBottomText;
        UpdateWallImageScaleValueLabel();
        UpdateWallImageOverlay();
        UpdateCropEditorOverlay();
    }

    private void UpdateWallImageOverlay()
    {
        var wall = viewModel.SelectedWall;
        var panel = viewModel.SelectedPanel;
        if (wall is null || panel is null || string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
        {
            WallImageOverlay.IsVisible = false;
            WallImageOverlay.Source = null;
            return;
        }

        var wallBounds = previewDrawable.GetWallBounds();
        var scale = Math.Max(0.01f, previewDrawable.PixelsPerMillimeter * previewDrawable.ZoomFactor);
        var panelBaseX = wallBounds.X + ((float)panel.X * scale);
        var panelBaseY = wallBounds.Y + ((float)panel.Y * scale);
        var imageWidth = ((float)panel.Width * scale) * (float)Math.Max(0.2d, panel.ImageScale);
        var imageHeight = ((float)panel.Height * scale) * (float)Math.Max(0.2d, panel.ImageScale);
        var cropWidthFactor = panel.EffectiveImageCropWidthFactor;
        var cropHeightFactor = panel.EffectiveImageCropHeightFactor;
        var stretchedWidth = imageWidth / (float)cropWidthFactor;
        var stretchedHeight = imageHeight / (float)cropHeightFactor;
        var imageX = panelBaseX + ((float)panel.ImageOffsetX * scale) - (float)(panel.EffectiveImageCropLeft * stretchedWidth);
        var imageY = panelBaseY + ((float)panel.ImageOffsetY * scale) - (float)(panel.EffectiveImageCropTop * stretchedHeight);

        WallImageOverlay.Source = ImageSource.FromFile(panel.ImagePath);
        WallImageOverlay.Opacity = panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity;
        WallImageOverlay.IsVisible = true;
        AbsoluteLayout.SetLayoutBounds(WallImageOverlay, new Rect(imageX, imageY, stretchedWidth, stretchedHeight));
        AbsoluteLayout.SetLayoutFlags(WallImageOverlay, AbsoluteLayoutFlags.None);
    }

    private void UpdateCropEditorOverlay()
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath) || CropEditorViewport.Width <= 0 || CropEditorViewport.Height <= 0)
        {
            cropEditorImageBounds = Rect.Zero;
            CropEditorImage.IsVisible = false;
            CropSelectionBorder.IsVisible = false;
            CropHandleTopLeft.IsVisible = false;
            CropHandleTopRight.IsVisible = false;
            CropHandleBottomLeft.IsVisible = false;
            CropHandleBottomRight.IsVisible = false;
            CropMaskTop.IsVisible = false;
            CropMaskBottom.IsVisible = false;
            CropMaskLeft.IsVisible = false;
            CropMaskRight.IsVisible = false;
            CropEditorEmptyLabel.IsVisible = true;
            return;
        }

        CropEditorImage.Source = ImageSource.FromFile(panel.ImagePath);
        CropEditorImage.IsVisible = true;
        CropEditorEmptyLabel.IsVisible = false;

        var imageBounds = GetCropEditorImageBounds(panel.ImagePath!, CropEditorViewport.Width, CropEditorViewport.Height);
        cropEditorImageBounds = imageBounds;

        AbsoluteLayout.SetLayoutBounds(CropEditorImage, imageBounds);
        AbsoluteLayout.SetLayoutFlags(CropEditorImage, AbsoluteLayoutFlags.None);

        var selectionX = imageBounds.X + (panel.EffectiveImageCropLeft * imageBounds.Width);
        var selectionY = imageBounds.Y + (panel.EffectiveImageCropTop * imageBounds.Height);
        var selectionWidth = panel.EffectiveImageCropWidthFactor * imageBounds.Width;
        var selectionHeight = panel.EffectiveImageCropHeightFactor * imageBounds.Height;
        var selectionRect = new Rect(selectionX, selectionY, selectionWidth, selectionHeight);

        CropSelectionBorder.IsVisible = true;
        AbsoluteLayout.SetLayoutBounds(CropSelectionBorder, selectionRect);
        AbsoluteLayout.SetLayoutFlags(CropSelectionBorder, AbsoluteLayoutFlags.None);

        UpdateCropHandle(CropHandleTopLeft, selectionRect.Left, selectionRect.Top);
        UpdateCropHandle(CropHandleTopRight, selectionRect.Right, selectionRect.Top);
        UpdateCropHandle(CropHandleBottomLeft, selectionRect.Left, selectionRect.Bottom);
        UpdateCropHandle(CropHandleBottomRight, selectionRect.Right, selectionRect.Bottom);

        UpdateCropMasks(imageBounds, selectionRect);
    }

    private PanelInput ReadPanelInput()
    {
        return new PanelInput
        {
            Name = PanelNameEntry.Text?.Trim() ?? string.Empty,
            X = ParseNonNegativeDouble(PanelXEntry.Text),
            Y = ParseNonNegativeDouble(PanelYEntry.Text),
            Width = ParsePositiveDouble(PanelWidthEntry.Text, "Controlla i valori del pannello e dei fori."),
            Height = ParsePositiveDouble(PanelHeightEntry.Text, "Controlla i valori del pannello e dei fori."),
            EdgeOffsetX = ParseNonNegativeDouble(HoleOffsetEntry.Text),
            EdgeOffsetY = ParseNonNegativeDouble(HoleOffsetYEntry.Text),
            HorizontalSpacing = ParsePositiveDouble(HoleHorizontalEntry.Text, "Controlla i valori del pannello e dei fori."),
            VerticalSpacing = ParsePositiveDouble(HoleVerticalEntry.Text, "Controlla i valori del pannello e dei fori.")
        };
    }

    private static double ParsePositiveDouble(string? text, string errorMessage)
    {
        var value = ParseDouble(text);
        if (value <= 0)
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value;
    }

    private static double ParseNonNegativeDouble(string? text)
    {
        var value = ParseDouble(text);
        if (value < 0)
        {
            throw new InvalidOperationException("Controlla i valori del pannello e dei fori.");
        }

        return value;
    }

    private static double ParseDouble(string? text)
    {
        var normalized = text?.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidOperationException("Controlla i valori numerici inseriti.");
    }

    private Task ShowError(string message)
    {
        return DisplayAlertAsync("Dati non validi", message, "OK");
    }

    private void AdjustWallImageScale(double delta)
    {
        var nextValue = Math.Clamp(WallImageScaleSlider.Value + delta, WallImageScaleSlider.Minimum, WallImageScaleSlider.Maximum);
        WallImageScaleSlider.Value = Math.Round(nextValue, 2, MidpointRounding.AwayFromZero);
        UpdateWallImageScaleValueLabel();
    }

    private void UpdateWallImageScaleValueLabel()
    {
        WallImageScaleValueLabel.Text = $"Scala attuale: {WallImageScaleSlider.Value:0.00}";
    }

    private async void OnResetWallImageCropClicked(object? sender, EventArgs e)
    {
        try
        {
            if (viewModel.SelectedPanel is null)
            {
                throw new InvalidOperationException("Seleziona prima un pannello.");
            }

            viewModel.UpdateSelectedPanelImageCrop(0d, 0d, 0d, 0d);
            ApplyWallImageEditorState();
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private void OnCropMovePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        HandleCropPanUpdated(CropDragMode.Move, e);
    }

    private void OnCropTopLeftPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        HandleCropPanUpdated(CropDragMode.TopLeft, e);
    }

    private void OnCropTopRightPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        HandleCropPanUpdated(CropDragMode.TopRight, e);
    }

    private void OnCropBottomLeftPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        HandleCropPanUpdated(CropDragMode.BottomLeft, e);
    }

    private void OnCropBottomRightPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        HandleCropPanUpdated(CropDragMode.BottomRight, e);
    }

    private void HandleCropPanUpdated(CropDragMode mode, PanUpdatedEventArgs e)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null || cropEditorImageBounds.Width <= 1 || cropEditorImageBounds.Height <= 1)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                cropDragMode = mode;
                cropDragStartLeft = panel.EffectiveImageCropLeft;
                cropDragStartTop = panel.EffectiveImageCropTop;
                cropDragStartRight = panel.EffectiveImageCropRight;
                cropDragStartBottom = panel.EffectiveImageCropBottom;
                break;

            case GestureStatus.Running:
                ApplyCropDragDelta(e.TotalX / cropEditorImageBounds.Width, e.TotalY / cropEditorImageBounds.Height);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                cropDragMode = CropDragMode.None;
                break;
        }
    }

    private void ApplyCropDragDelta(double deltaXRatio, double deltaYRatio)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null)
        {
            return;
        }

        const double minFactor = 0.001d;
        double left = cropDragStartLeft;
        double top = cropDragStartTop;
        double right = cropDragStartRight;
        double bottom = cropDragStartBottom;

        switch (cropDragMode)
        {
            case CropDragMode.Move:
            {
                var width = 1d - left - right;
                var height = 1d - top - bottom;
                left = Math.Clamp(cropDragStartLeft + deltaXRatio, 0d, 1d - width);
                top = Math.Clamp(cropDragStartTop + deltaYRatio, 0d, 1d - height);
                right = 1d - width - left;
                bottom = 1d - height - top;
                break;
            }

            case CropDragMode.TopLeft:
                left = Math.Clamp(cropDragStartLeft + deltaXRatio, 0d, 1d - right - minFactor);
                top = Math.Clamp(cropDragStartTop + deltaYRatio, 0d, 1d - bottom - minFactor);
                break;

            case CropDragMode.TopRight:
                right = Math.Clamp(cropDragStartRight - deltaXRatio, 0d, 1d - left - minFactor);
                top = Math.Clamp(cropDragStartTop + deltaYRatio, 0d, 1d - bottom - minFactor);
                break;

            case CropDragMode.BottomLeft:
                left = Math.Clamp(cropDragStartLeft + deltaXRatio, 0d, 1d - right - minFactor);
                bottom = Math.Clamp(cropDragStartBottom - deltaYRatio, 0d, 1d - top - minFactor);
                break;

            case CropDragMode.BottomRight:
                right = Math.Clamp(cropDragStartRight - deltaXRatio, 0d, 1d - left - minFactor);
                bottom = Math.Clamp(cropDragStartBottom - deltaYRatio, 0d, 1d - top - minFactor);
                break;
        }

        try
        {
            viewModel.UpdateSelectedPanelImageCrop(left, top, right, bottom);
            SyncCropEntriesFromPanel(panel);
            UpdateWallImageOverlay();
            UpdateCropEditorOverlay();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void TryApplyLiveWallImageAlignment()
    {
        if (viewModel.SelectedPanel is null)
        {
            return;
        }

        if (!TryParseDouble(WallImageOffsetXEntry.Text, out var offsetX) ||
            !TryParseDouble(WallImageOffsetYEntry.Text, out var offsetY) ||
            !TryParsePercent(WallImageCropLeftEntry.Text, out var cropLeft) ||
            !TryParsePercent(WallImageCropTopEntry.Text, out var cropTop) ||
            !TryParsePercent(WallImageCropRightEntry.Text, out var cropRight) ||
            !TryParsePercent(WallImageCropBottomEntry.Text, out var cropBottom))
        {
            return;
        }

        try
        {
            viewModel.UpdateSelectedPanelImageCrop(cropLeft, cropTop, cropRight, cropBottom);
            viewModel.UpdateSelectedPanelImageAlignment(offsetX, offsetY, WallImageScaleSlider.Value, WallImageOpacitySlider.Value);
            UpdateWallImageOverlay();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        var normalized = text?.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParsePercent(string? text, out double value)
    {
        if (!TryParseDouble(text, out var percent))
        {
            value = 0d;
            return false;
        }

        value = percent / 100d;
        return value >= 0d && value <= 99.9d / 100d;
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

    private void SyncCropEntriesFromPanel(PanelDefinition panel)
    {
        WallImageCropLeftEntry.Text = (panel.EffectiveImageCropLeft * 100d).ToString("0.##", CultureInfo.InvariantCulture);
        WallImageCropTopEntry.Text = (panel.EffectiveImageCropTop * 100d).ToString("0.##", CultureInfo.InvariantCulture);
        WallImageCropRightEntry.Text = (panel.EffectiveImageCropRight * 100d).ToString("0.##", CultureInfo.InvariantCulture);
        WallImageCropBottomEntry.Text = (panel.EffectiveImageCropBottom * 100d).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void UpdateCropHandle(VisualElement handle, double centerX, double centerY)
    {
        handle.IsVisible = true;
        var width = handle.WidthRequest > 0 ? handle.WidthRequest : 24d;
        var height = handle.HeightRequest > 0 ? handle.HeightRequest : 24d;
        AbsoluteLayout.SetLayoutBounds(handle, new Rect(centerX - (width / 2d), centerY - (height / 2d), width, height));
        AbsoluteLayout.SetLayoutFlags(handle, AbsoluteLayoutFlags.None);
    }

    private void UpdateCropMasks(Rect imageBounds, Rect selectionRect)
    {
        SetMaskBounds(CropMaskTop, new Rect(imageBounds.X, imageBounds.Y, imageBounds.Width, Math.Max(0d, selectionRect.Y - imageBounds.Y)));
        SetMaskBounds(CropMaskBottom, new Rect(imageBounds.X, selectionRect.Bottom, imageBounds.Width, Math.Max(0d, imageBounds.Bottom - selectionRect.Bottom)));
        SetMaskBounds(CropMaskLeft, new Rect(imageBounds.X, selectionRect.Y, Math.Max(0d, selectionRect.X - imageBounds.X), selectionRect.Height));
        SetMaskBounds(CropMaskRight, new Rect(selectionRect.Right, selectionRect.Y, Math.Max(0d, imageBounds.Right - selectionRect.Right), selectionRect.Height));
    }

    private static void SetMaskBounds(BoxView mask, Rect rect)
    {
        mask.IsVisible = rect.Width > 0.5d && rect.Height > 0.5d;
        AbsoluteLayout.SetLayoutBounds(mask, rect);
        AbsoluteLayout.SetLayoutFlags(mask, AbsoluteLayoutFlags.None);
    }

    private Rect GetCropEditorImageBounds(string imagePath, double viewportWidth, double viewportHeight)
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
            var options = new Android.Graphics.BitmapFactory.Options
            {
                InJustDecodeBounds = true
            };

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

    private static View BuildErrorView(string title, Exception ex)
    {
        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 22
                    },
                    new Label
                    {
                        Text = ex.ToString()
                    }
                }
            }
        };
    }
}
