using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using RuoteLab.Drawing;
using RuoteLab.Models;
using RuoteLab.ViewModels;

namespace RuoteLab;

public partial class GymSetupPage : ContentPage
{
    private enum WorkspaceSection
    {
        Setup,
        Panels,
        Preview,
        Image,
        Output
    }

    private readonly Services.IGymSetupEditorStateService editorStateService;
    private readonly Services.IGymSetupPageStateService pageStateService;
    private readonly GymSetupViewModel viewModel;
    private readonly LayoutPreviewDrawable previewDrawable;
    private double previewZoom = 1d;
    private double previewZoomStart = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;
    private bool isSyncingSelection;
    private bool isWallEditorExpanded;
    private bool isPanelEditorExpanded;
    private WorkspaceSection activeWorkspaceSection = WorkspaceSection.Setup;
    private readonly IReadOnlyList<LedRoutingAxis> availableRoutingAxes = Enum.GetValues<LedRoutingAxis>();
    private IReadOnlyList<LedStartDirection> availableStartDirections = Array.Empty<LedStartDirection>();

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

            RoomsPicker.ItemsSource = viewModel.Rooms;
            LedRoutingAxisPicker.ItemsSource = availableRoutingAxes.Select(PanelDefinition.GetAxisLabel).ToList();
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
            await viewModel.EnsureLoadedAsync();
            ApplyWallEditorState(useSelectedWallValues: viewModel.SelectedWall is not null);
            ApplyPanelEditorState(resetToDefaults: true);
            activeWorkspaceSection = ResolveSuggestedWorkspaceSection();
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

            isWallEditorExpanded = false;
            isPanelEditorExpanded = false;
            ApplyWallEditorDefaults();
            ApplyPanelEditorState(resetToDefaults: true);
            activeWorkspaceSection = WorkspaceSection.Panels;
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
            isWallEditorExpanded = false;
            isPanelEditorExpanded = false;
            ApplyWallEditorDefaults();
            ApplyPanelEditorState(resetToDefaults: true);
            activeWorkspaceSection = WorkspaceSection.Setup;
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

            isWallEditorExpanded = false;
            isPanelEditorExpanded = false;
            ApplyWallEditorState(useSelectedWallValues: true);
            ApplyPanelEditorState(resetToDefaults: true);
            activeWorkspaceSection = WorkspaceSection.Panels;
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
            isPanelEditorExpanded = false;
            ApplyPanelEditorState(resetToDefaults: true);
            activeWorkspaceSection = WorkspaceSection.Preview;
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
            isPanelEditorExpanded = false;
            ApplyPanelEditorState();
            activeWorkspaceSection = WorkspaceSection.Preview;
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
            isPanelEditorExpanded = false;
            ApplyPanelEditorState(resetToDefaults: true);
            activeWorkspaceSection = WorkspaceSection.Panels;
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

    private void OnWallSelectionChanged(object? sender, EventArgs e)
    {
        if (isSyncingSelection)
        {
            return;
        }

        viewModel.SelectWall(WallsPicker.SelectedItem as WallDefinition);
        isWallEditorExpanded = false;
        isPanelEditorExpanded = false;
        ApplyWallEditorState(useSelectedWallValues: viewModel.SelectedWall is not null);
        ApplyPanelEditorState(resetToDefaults: true);
        activeWorkspaceSection = viewModel.SelectedWall is null ? WorkspaceSection.Setup : WorkspaceSection.Panels;
        SyncViewFromState();
    }

    private void OnRoomSelectionChanged(object? sender, EventArgs e)
    {
        if (isSyncingSelection)
        {
            return;
        }

        viewModel.SelectRoom(RoomsPicker.SelectedItem as RoomDefinition);
        isWallEditorExpanded = false;
        isPanelEditorExpanded = false;
        ApplyWallEditorDefaults();
        ApplyPanelEditorState(resetToDefaults: true);
        activeWorkspaceSection = WorkspaceSection.Setup;
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

    private void OnLedRoutingAxisChanged(object? sender, EventArgs e)
    {
        var axis = GetSelectedLedRoutingAxis();
        RefreshLedStartDirectionPicker(axis, selectedDirection: GetDefaultDirection(axis));
    }

    private void SyncViewFromState()
    {
        isSyncingSelection = true;
        var pageState = pageStateService.Build(viewModel);
        activeWorkspaceSection = NormalizeWorkspaceSection(activeWorkspaceSection, pageState);
        RoomsPicker.SelectedItem = pageState.SelectedRoom;
        WallsPicker.ItemsSource = pageState.VisibleWalls.ToList();
        WallsPicker.SelectedItem = pageState.SelectedWall;
        isSyncingSelection = false;

        WallInfoLabel.Text = pageState.WallInfoText;
        WorkflowTitleLabel.Text = pageState.WorkflowTitleText;
        WorkflowMessageLabel.Text = pageState.WorkflowMessageText;
        ActiveRoomContextLabel.Text = pageState.ActiveRoomText;
        ActiveWallContextLabel.Text = pageState.ActiveWallText;
        ActivePanelContextLabel.Text = pageState.ActivePanelText;
        NextActionLabel.Text = pageState.NextActionText;
        RoomSummaryLabel.Text = pageState.RoomSummaryText;
        WallSelectionHintLabel.Text = pageState.WallSelectionHintText;
        PanelEditorModeLabel.Text = pageState.PanelEditorModeText;
        SelectedPanelSummaryLabel.Text = pageState.SelectedPanelSummaryText;
        PanelsEmptyLabel.IsVisible = pageState.ShowEmptyPanels;
        WallImageInfoLabel.Text = pageState.WallImageInfoText;
        AddWallButton.IsEnabled = pageState.CanAddWall;
        UpdateWallButton.IsEnabled = viewModel.HasSelectedWall;
        NewWallButton.IsEnabled = viewModel.SelectedRoom is not null;
        PanelsSectionBorder.IsEnabled = pageState.CanEditPanels;
        WallImageSectionBorder.IsEnabled = pageState.CanManageWallImage;
        OpenPanelImagePageButton.IsEnabled = pageState.CanManageWallImage;
        PreviewGoToImageButton.IsEnabled = pageState.CanManageWallImage;
        ImageGoToOutputButton.IsEnabled = pageState.CanSaveWall;
        SaveWallButton.IsEnabled = pageState.CanSaveWall;
        OpenHardwareMappingButton.IsEnabled = viewModel.HasSelectedWall;
        QuickOpenHardwareMappingButton.IsEnabled = viewModel.HasSelectedWall;
        QuickSaveWallButton.IsEnabled = pageState.CanSaveWall;
        Title = viewModel.SelectedWall is null ? "Dettaglio parete" : $"Parete · {viewModel.SelectedWall.Name}";
        WallSummaryLabel.Text = viewModel.SelectedWall is null
            ? "Nessuna parete selezionata."
            : $"{viewModel.SelectedWall.Name} · {viewModel.SelectedWall.Width:0.#} x {viewModel.SelectedWall.Height:0.#} mm";
        WallPanelsSummaryLabel.Text = viewModel.SelectedWall is null
            ? "0 pannelli"
            : viewModel.SelectedWall.Panels.Count == 1
                ? "1 pannello"
                : $"{viewModel.SelectedWall.Panels.Count} pannelli";
        ApplyWorkspaceVisibility(pageState);
        UpdateWorkspaceButtons();
        UpdateWallEditorVisibility();
        UpdatePanelEditorVisibility();

        previewDrawable.Wall = viewModel.SelectedWall;
        previewDrawable.SelectedPanel = viewModel.SelectedPanel;

        RebuildPanelsList();
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
        UpdateEditorButtons();
        UpdateWallImageOverlay();
    }

    private WorkspaceSection ResolveSuggestedWorkspaceSection()
    {
        if (viewModel.SelectedRoom is null)
        {
            return WorkspaceSection.Setup;
        }

        if (viewModel.SelectedWall is null)
        {
            return WorkspaceSection.Setup;
        }

        if (!viewModel.SelectedWall.Panels.Any())
        {
            return WorkspaceSection.Panels;
        }

        return WorkspaceSection.Panels;
    }

    private WorkspaceSection NormalizeWorkspaceSection(WorkspaceSection requestedSection, GymSetupPageState pageState)
    {
        return requestedSection switch
        {
            WorkspaceSection.Panels when !pageState.CanEditPanels => WorkspaceSection.Setup,
            WorkspaceSection.Preview when !pageState.CanEditPanels => WorkspaceSection.Setup,
            WorkspaceSection.Image when !pageState.CanManageWallImage => pageState.CanEditPanels ? WorkspaceSection.Panels : WorkspaceSection.Setup,
            WorkspaceSection.Output when !pageState.CanSaveWall => pageState.CanEditPanels ? WorkspaceSection.Panels : WorkspaceSection.Setup,
            _ => requestedSection
        };
    }

    private void ApplyWorkspaceVisibility(GymSetupPageState pageState)
    {
        var isSetupVisible = activeWorkspaceSection == WorkspaceSection.Setup;
        var isPanelsVisible = activeWorkspaceSection == WorkspaceSection.Panels;
        var isPreviewVisible = activeWorkspaceSection == WorkspaceSection.Preview;
        var isImageVisible = activeWorkspaceSection == WorkspaceSection.Image;
        var isOutputVisible = activeWorkspaceSection == WorkspaceSection.Output;

        RoomSectionBorder.IsVisible = false;
        WallSectionBorder.IsVisible = isSetupVisible;
        PanelsSectionBorder.IsVisible = isPanelsVisible;
        PreviewSectionBorder.IsVisible = isPreviewVisible;
        WallImageSectionBorder.IsVisible = isImageVisible;
        OutputSectionBorder.IsVisible = isOutputVisible;

        PanelsSectionBorder.IsEnabled = pageState.CanEditPanels;
        PreviewSectionBorder.IsEnabled = pageState.CanEditPanels;
        WallImageSectionBorder.IsEnabled = pageState.CanManageWallImage;
        OutputSectionBorder.IsEnabled = pageState.CanSaveWall;
    }

    private void UpdateWorkspaceButtons()
    {
        ApplyWorkspaceButtonState(SetupWorkspaceButton, activeWorkspaceSection == WorkspaceSection.Setup);
        ApplyWorkspaceButtonState(PanelsWorkspaceButton, activeWorkspaceSection == WorkspaceSection.Panels);
        ApplyWorkspaceButtonState(PreviewWorkspaceButton, activeWorkspaceSection == WorkspaceSection.Preview);
        ApplyWorkspaceButtonState(ImageWorkspaceButton, activeWorkspaceSection == WorkspaceSection.Image);
        ApplyWorkspaceButtonState(OutputWorkspaceButton, activeWorkspaceSection == WorkspaceSection.Output);

        PanelsWorkspaceButton.IsEnabled = viewModel.HasSelectedWall;
        PreviewWorkspaceButton.IsEnabled = viewModel.HasSelectedWall;
        ImageWorkspaceButton.IsEnabled = viewModel.HasSelectedPanel;
        OutputWorkspaceButton.IsEnabled = viewModel.HasSelectedWall;
    }

    private void UpdatePanelEditorVisibility()
    {
        var canEditPanels = viewModel.HasSelectedWall;
        PanelEditorContainer.IsVisible = canEditPanels && isPanelEditorExpanded;
        NewPanelButton.IsEnabled = canEditPanels;
        TogglePanelEditorButton.IsEnabled = canEditPanels;
        TogglePanelEditorButton.Text = isPanelEditorExpanded ? "Chiudi editor pannello" : "Apri editor pannello";
    }

    private void UpdateWallEditorVisibility()
    {
        var canEditWall = viewModel.SelectedRoom is not null;
        WallEditorContainer.IsVisible = canEditWall && isWallEditorExpanded;
        ToggleWallEditorButton.IsEnabled = canEditWall;
        ToggleWallEditorButton.Text = isWallEditorExpanded ? "Nascondi editor" : "Mostra editor";
    }

    private void ApplyWorkspaceButtonState(Button button, bool isActive)
    {
        button.BackgroundColor = isActive ? Color.FromArgb("#F2C94C") : Color.FromArgb("#211C14");
        button.TextColor = isActive ? Color.FromArgb("#14110B") : Color.FromArgb("#F8E7A8");
        button.BorderColor = Color.FromArgb("#B9922F");
        button.BorderWidth = 1;
    }

    private void OnSetupWorkspaceClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Setup;
        SyncViewFromState();
    }

    private void OnPanelsWorkspaceClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Panels;
        if (viewModel.SelectedWall is not null && !viewModel.SelectedWall.Panels.Any())
        {
            isPanelEditorExpanded = true;
            viewModel.ClearSelectedPanel();
            ApplyPanelEditorState(resetToDefaults: true);
        }
        SyncViewFromState();
    }

    private void OnPreviewWorkspaceClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Preview;
        SyncViewFromState();
    }

    private void OnImageWorkspaceClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Image;
        SyncViewFromState();
    }

    private void OnOutputWorkspaceClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Output;
        SyncViewFromState();
    }

    private void OnNewWallClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedRoom is null)
        {
            return;
        }

        isWallEditorExpanded = true;
        viewModel.SelectWall(null);
        ApplyWallEditorDefaults();
        SyncViewFromState();
    }

    private void OnToggleWallEditorClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedRoom is null)
        {
            return;
        }

        isWallEditorExpanded = !isWallEditorExpanded;
        if (isWallEditorExpanded)
        {
            ApplyWallEditorState(useSelectedWallValues: viewModel.SelectedWall is not null);
        }

        SyncViewFromState();
    }

    private void OnPreviewGoToImageClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Image;
        SyncViewFromState();
    }

    private void OnPreviewGoToOutputClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Output;
        SyncViewFromState();
    }

    private void OnImageGoToOutputClicked(object? sender, EventArgs e)
    {
        activeWorkspaceSection = WorkspaceSection.Output;
        SyncViewFromState();
    }

    private void OnNewPanelClicked(object? sender, EventArgs e)
    {
        if (!viewModel.HasSelectedWall)
        {
            return;
        }

        isPanelEditorExpanded = true;
        viewModel.ClearSelectedPanel();
        ApplyPanelEditorState(resetToDefaults: true);
        SyncViewFromState();
    }

    private void OnTogglePanelEditorClicked(object? sender, EventArgs e)
    {
        if (!viewModel.HasSelectedWall)
        {
            return;
        }

        isPanelEditorExpanded = !isPanelEditorExpanded;
        if (isPanelEditorExpanded && !viewModel.HasSelectedPanel)
        {
            ApplyPanelEditorState(resetToDefaults: true);
        }

        SyncViewFromState();
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
        ApplyLedRoutingEditorState(editorState.LedRoutingAxis, editorState.LedStartDirection);
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
            var selectButton = new Button
            {
                Text = isSelected ? "Pannello selezionato" : "Seleziona",
                Style = (Style)Application.Current!.Resources[isSelected ? "SecondaryActionButtonStyle" : "PrimaryActionButtonStyle"]
            };
            selectButton.Clicked += (_, _) =>
            {
                viewModel.SelectPanel(panel);
                isPanelEditorExpanded = true;
                ApplyPanelEditorState();
                activeWorkspaceSection = WorkspaceSection.Panels;
                SyncViewFromState();
            };

            var imageButton = new Button
            {
                Text = "Apri immagine",
                Style = (Style)Application.Current!.Resources["SecondaryActionButtonStyle"]
            };
            imageButton.Clicked += (_, _) =>
            {
                viewModel.SelectPanel(panel);
                ApplyPanelEditorState();
                _ = Shell.Current.GoToAsync("panel-image-page");
            };

            var cropButton = new Button
            {
                Text = "Ritaglio",
                Style = (Style)Application.Current!.Resources["SecondaryActionButtonStyle"]
            };
            cropButton.Clicked += async (_, _) =>
            {
                viewModel.SelectPanel(panel);
                ApplyPanelEditorState();
                activeWorkspaceSection = WorkspaceSection.Image;
                SyncViewFromState();
                await OnOpenCropEditorForSelectedPanelAsync();
            };

            var border = new Border
            {
                Background = isSelected ? Color.FromArgb("#2A2212") : Color.FromArgb("#191611"),
                Stroke = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#B9922F"),
                StrokeThickness = isSelected ? 3 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = 12
            };

            var actionsGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            actionsGrid.Add(selectButton);
            Grid.SetColumn(selectButton, 0);
            actionsGrid.Add(imageButton);
            Grid.SetColumn(imageButton, 1);
            actionsGrid.Add(cropButton);
            Grid.SetColumn(cropButton, 2);

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
                    },
                    actionsGrid
                }
            };

            PanelsHost.Children.Add(border);
        }
    }

    private async void OnBackToWallsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnOpenHardwareMappingClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new HardwareMappingPage());
    }

    private async void OnOpenCropEditorClicked(object? sender, EventArgs e)
    {
        await OnOpenCropEditorForSelectedPanelAsync();
    }

    private async void OnOpenPanelImagePageClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            await ShowError("Seleziona prima un pannello.");
            return;
        }

        await Shell.Current.GoToAsync("panel-image-page");
    }

    private async Task OnOpenCropEditorForSelectedPanelAsync()
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

    private PanelInput ReadPanelInput()
    {
        var axis = GetSelectedLedRoutingAxis();
        var direction = GetSelectedLedStartDirection();
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
            VerticalSpacing = ParsePositiveDouble(HoleVerticalEntry.Text, "Controlla i valori del pannello e dei fori."),
            LedRoutingAxis = axis,
            LedStartDirection = direction
        };
    }

    private void ApplyLedRoutingEditorState(LedRoutingAxis axis, LedStartDirection direction)
    {
        LedRoutingAxisPicker.SelectedIndex = (int)axis;
        RefreshLedStartDirectionPicker(axis, direction);
    }

    private void RefreshLedStartDirectionPicker(LedRoutingAxis axis, LedStartDirection selectedDirection)
    {
        availableStartDirections = GetDirectionsForAxis(axis);
        LedStartDirectionPicker.ItemsSource = availableStartDirections
            .Select(PanelDefinition.GetDirectionLabel)
            .ToList();

        var selectedIndex = availableStartDirections
            .Select((direction, index) => new { direction, index })
            .FirstOrDefault(item => item.direction == selectedDirection)?.index ?? -1;
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        LedStartDirectionPicker.SelectedIndex = selectedIndex;
    }

    private static IReadOnlyList<LedStartDirection> GetDirectionsForAxis(LedRoutingAxis axis)
    {
        return axis == LedRoutingAxis.Horizontal
            ? new[] { LedStartDirection.LeftToRight, LedStartDirection.RightToLeft }
            : new[] { LedStartDirection.BottomToTop, LedStartDirection.TopToBottom };
    }

    private LedRoutingAxis GetSelectedLedRoutingAxis()
    {
        if (LedRoutingAxisPicker.SelectedIndex < 0 || LedRoutingAxisPicker.SelectedIndex >= availableRoutingAxes.Count)
        {
            return LedRoutingAxis.Vertical;
        }

        return availableRoutingAxes[LedRoutingAxisPicker.SelectedIndex];
    }

    private LedStartDirection GetSelectedLedStartDirection()
    {
        if (LedStartDirectionPicker.SelectedIndex < 0 || LedStartDirectionPicker.SelectedIndex >= availableStartDirections.Count)
        {
            return GetDefaultDirection(GetSelectedLedRoutingAxis());
        }

        return availableStartDirections[LedStartDirectionPicker.SelectedIndex];
    }

    private static LedStartDirection GetDefaultDirection(LedRoutingAxis axis)
    {
        return axis == LedRoutingAxis.Horizontal
            ? LedStartDirection.LeftToRight
            : LedStartDirection.BottomToTop;
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
