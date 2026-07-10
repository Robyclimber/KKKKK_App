using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using WallPanelPlanner.Drawing;
using WallPanelPlanner.Models;
using WallPanelPlanner.ViewModels;

namespace WallPanelPlanner;

public partial class GymSetupPage : ContentPage
{
    private readonly Services.IGymSetupEditorStateService editorStateService;
    private readonly Services.IGymSetupPageStateService pageStateService;
    private readonly GymSetupViewModel viewModel;
    private readonly LayoutPreviewDrawable previewDrawable;
    private readonly Services.IWallImageService wallImageService;
    private double previewZoom = 1d;
    private double previewZoomStart = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;
    private bool isSyncingSelection;

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
            Title = "Errore Configurazione";
            Content = BuildErrorView("Errore inizializzazione GymSetupPage", ex);
        }
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;
        await InitializeAsync();
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
            if (viewModel.SelectedWall is null)
            {
                throw new InvalidOperationException("Seleziona prima una parete.");
            }

            var importedPath = await wallImageService.PickAndImportImageAsync();
            if (string.IsNullOrWhiteSpace(importedPath))
            {
                return;
            }

            viewModel.SetSelectedWallImage(importedPath);
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
            viewModel.ClearSelectedWallImage();
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

            if (string.IsNullOrWhiteSpace(viewModel.SelectedWall.ImagePath) || !File.Exists(viewModel.SelectedWall.ImagePath))
            {
                throw new InvalidOperationException("Carica prima una foto della parete.");
            }

            await Navigation.PushAsync(new HoldAnalysisPage(viewModel, ((App)Application.Current!).WallConfigurationStorageService, viewModel.SelectedWall));
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
            viewModel.UpdateSelectedWallImageAlignment(
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
        SaveWallButton.IsEnabled = pageState.CanSaveWall;

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

    private void ApplyWallImageEditorState()
    {
        var wall = viewModel.SelectedWall;
        var pageState = pageStateService.Build(viewModel);
        WallImageOffsetXEntry.Text = pageState.WallImageOffsetXText;
        WallImageOffsetYEntry.Text = pageState.WallImageOffsetYText;
        WallImageScaleSlider.Value = pageState.WallImageScale;
        WallImageOpacitySlider.Value = pageState.WallImageOpacity;
        UpdateWallImageScaleValueLabel();
        UpdateWallImageOverlay();
    }

    private void UpdateWallImageOverlay()
    {
        var wall = viewModel.SelectedWall;
        if (wall is null || string.IsNullOrWhiteSpace(wall.ImagePath) || !File.Exists(wall.ImagePath))
        {
            WallImageOverlay.IsVisible = false;
            WallImageOverlay.Source = null;
            return;
        }

        var wallBounds = previewDrawable.GetWallBounds();
        var scale = Math.Max(0.01f, previewDrawable.PixelsPerMillimeter * previewDrawable.ZoomFactor);
        var imageWidth = wallBounds.Width * (float)Math.Max(0.2d, wall.ImageScale);
        var imageHeight = wallBounds.Height * (float)Math.Max(0.2d, wall.ImageScale);
        var imageX = wallBounds.X + ((float)wall.ImageOffsetX * scale);
        var imageY = wallBounds.Y + ((float)wall.ImageOffsetY * scale);

        WallImageOverlay.Source = ImageSource.FromFile(wall.ImagePath);
        WallImageOverlay.Opacity = wall.ImageOpacity <= 0 ? 0.55d : wall.ImageOpacity;
        WallImageOverlay.IsVisible = true;
        AbsoluteLayout.SetLayoutBounds(WallImageOverlay, new Rect(imageX, imageY, imageWidth, imageHeight));
        AbsoluteLayout.SetLayoutFlags(WallImageOverlay, AbsoluteLayoutFlags.None);
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

    private void TryApplyLiveWallImageAlignment()
    {
        if (viewModel.SelectedWall is null)
        {
            return;
        }

        if (!TryParseDouble(WallImageOffsetXEntry.Text, out var offsetX) ||
            !TryParseDouble(WallImageOffsetYEntry.Text, out var offsetY))
        {
            return;
        }

        viewModel.UpdateSelectedWallImageAlignment(offsetX, offsetY, WallImageScaleSlider.Value, WallImageOpacitySlider.Value);
        UpdateWallImageOverlay();
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        var normalized = text?.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
