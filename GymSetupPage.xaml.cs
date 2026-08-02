using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using RouteLab.Drawing;
using RouteLab.Models;
using RouteLab.ViewModels;

namespace RouteLab;

public partial class GymSetupPage : ContentPage
{
    private readonly Services.IGymSetupEditorStateService editorStateService;
    private readonly Services.IGymSetupPageStateService pageStateService;
    private readonly GymSetupViewModel viewModel;
    private readonly LayoutPreviewDrawable previewDrawable;
    private double previewZoom = 1d;
    private double previewZoomStart = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;
    private bool isWallEditorExpanded;

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

            PreviewCanvas.Drawable = previewDrawable;

            ApplyWallEditorState(useSelectedWallValues: viewModel.SelectedWall is not null);
            Loaded += OnPageLoaded;
        }
        catch (Exception ex)
        {
            var databaseFactory = new Persistence.SqliteDatabaseFactory();
            var busyIndicatorService = ((App)Application.Current!).BusyIndicatorService;
            var wallRepository = new Services.SqliteWallRepository(databaseFactory, busyIndicatorService);
            var roomRepository = new Services.SqliteRoomRepository(databaseFactory, busyIndicatorService);
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
        using var busy = AppBusy.Show("Caricamento parete...");
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
            SyncViewFromState();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Configura palestra", $"Errore inizializzazione Configura palestra: {ex.Message}", "OK");
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
            ApplyWallEditorState(useSelectedWallValues: true);
            SyncViewFromState();
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void OnSaveWallClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Salvataggio parete...");
        try
        {
            if (isWallEditorExpanded)
            {
                viewModel.UpdateSelectedWall(new WallInput
                {
                    Name = WallNameEntry.Text?.Trim() ?? string.Empty,
                    Width = ParsePositiveDouble(WallWidthEntry.Text, "Inserisci larghezza e altezza valide per la parete."),
                    Height = ParsePositiveDouble(WallHeightEntry.Text, "Inserisci larghezza e altezza valide per la parete.")
                });
                ApplyWallEditorState(useSelectedWallValues: true);
            }

            var result = await viewModel.SaveSelectedWallAsync();
            isWallEditorExpanded = false;
            SyncViewFromState();
            await DisplayAlertAsync("Salvataggio completato", $"Parete salvata su database.\n{result}", "OK");
        }
        catch (InvalidOperationException ex)
        {
            await ShowError(ex.Message);
        }
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

    private void SyncViewFromState()
    {
        var pageState = pageStateService.Build(viewModel);

        WallInfoLabel.Text = pageState.WallInfoText;
        ActiveRoomContextLabel.Text = pageState.ActiveRoomText;
        ActiveWallContextLabel.Text = pageState.ActiveWallText;
        PanelsEmptyLabel.IsVisible = pageState.ShowEmptyPanels;
        PanelsCountLabel.Text = viewModel.SelectedWall?.Panels.Count == 1
            ? "1 pannello"
            : $"{viewModel.SelectedWall?.Panels.Count ?? 0} pannelli";
        WallActions.CanAdd = viewModel.HasSelectedWall;
        WallActions.CanSave = pageState.CanSaveWall;
        PreviewSectionBorder.IsEnabled = pageState.CanEditPanels;
        OpenHardwareMappingButton.IsEnabled = viewModel.HasSelectedWall;
        Title = viewModel.SelectedWall is null ? "Dettaglio parete" : $"Parete - {viewModel.SelectedWall.Name}";
        UpdateWallEditorVisibility();

        previewDrawable.Wall = viewModel.SelectedWall;
        previewDrawable.SelectedPanel = viewModel.SelectedPanel;

        RebuildPanelsList();
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
        UpdatePanelImageOverlays();
    }

    private void UpdateWallEditorVisibility()
    {
        var canEditWall = viewModel.HasSelectedWall;
        WallEditorContainer.IsVisible = canEditWall && isWallEditorExpanded;
        ToggleWallEditorButton.IsEnabled = canEditWall;
        ToggleWallEditorButton.Text = isWallEditorExpanded ? "Chiudi modifica" : "Modifica parete";
    }

    private void OnToggleWallEditorClicked(object? sender, EventArgs e)
    {
        if (!viewModel.HasSelectedWall)
        {
            return;
        }

        isWallEditorExpanded = !isWallEditorExpanded;
        if (isWallEditorExpanded)
        {
            ApplyWallEditorState(useSelectedWallValues: true);
        }

        SyncViewFromState();
    }

    private async void OnNewPanelClicked(object? sender, EventArgs e)
    {
        if (!viewModel.HasSelectedWall)
        {
            return;
        }

        viewModel.ClearSelectedPanel();
        await Shell.Current.GoToAsync("panel-detail-page");
    }

    private void ApplyWallEditorState(bool useSelectedWallValues)
    {
        var editorState = editorStateService.BuildWallEditor(viewModel, useSelectedWallValues);
        WallNameEntry.Text = editorState.WallNameText;
        WallWidthEntry.Text = editorState.WallWidthText;
        WallHeightEntry.Text = editorState.WallHeightText;
    }

    private void UpdatePreviewZoomLayout()
    {
        previewDrawable.ZoomFactor = (float)previewZoom;
        var desiredSize = previewDrawable.GetDesiredSize(previewZoom);
        PreviewCanvas.WidthRequest = Math.Max(basePreviewWidth, desiredSize.Width);
        PreviewCanvas.HeightRequest = Math.Max(basePreviewHeight, desiredSize.Height);
        PreviewLayer.WidthRequest = PreviewCanvas.WidthRequest;
        PreviewLayer.HeightRequest = PreviewCanvas.HeightRequest;
        UpdatePanelImageOverlays();
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
                Text = "Apri pannello",
                Style = (Style)Application.Current!.Resources["PrimaryActionButtonStyle"]
            };
            selectButton.Clicked += async (_, _) =>
            {
                viewModel.SelectPanel(panel);
                await Shell.Current.GoToAsync("panel-detail-page");
            };

            var border = new Border
            {
                Background = isSelected ? Color.FromArgb("#2A2212") : Color.FromArgb("#191611"),
                Stroke = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#B9922F"),
                StrokeThickness = isSelected ? 3 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
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
                        Text = panel.IsImageRectified &&
                               !string.IsNullOrWhiteSpace(panel.ImagePath) &&
                               File.Exists(panel.ImagePath)
                            ? "Immagine rettificata pronta"
                            : !string.IsNullOrWhiteSpace(panel.ImagePath) && File.Exists(panel.ImagePath)
                                ? "Foto caricata, da rettificare"
                                : "Nessuna foto associata",
                        FontSize = 12,
                        TextColor = panel.IsImageRectified
                            ? Color.FromArgb("#8FD694")
                            : Color.FromArgb("#B9AA79")
                    },
                    new Label
                    {
                        Text = isSelected ? "Ultimo pannello aperto" : "Disponibile",
                        FontSize = 12,
                        TextColor = isSelected ? Color.FromArgb("#F2C94C") : Color.FromArgb("#B9AA79")
                    },
                    selectButton
                }
            };

            PanelsHost.Children.Add(border);
        }
    }

    private async void OnOpenHardwareMappingClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new HardwareMappingPage());
    }

    private void UpdatePanelImageOverlays()
    {
        var wall = viewModel.SelectedWall;
        PanelImagesLayer.Children.Clear();
        if (wall is null)
        {
            return;
        }

        var wallBounds = previewDrawable.GetWallBounds();
        var scale = Math.Max(0.01f, previewDrawable.PixelsPerMillimeter * previewDrawable.ZoomFactor);

        foreach (var panel in wall.Panels.Where(panel =>
                     !string.IsNullOrWhiteSpace(panel.ImagePath) &&
                     File.Exists(panel.ImagePath)))
        {
            var panelX = wallBounds.X + ((float)panel.X * scale);
            var panelY = wallBounds.Y + ((float)panel.Y * scale);
            var panelWidth = (float)panel.Width * scale;
            var panelHeight = (float)panel.Height * scale;
            Rect imageBounds;

            if (panel.IsImageRectified)
            {
                imageBounds = new Rect(panelX, panelY, panelWidth, panelHeight);
            }
            else
            {
                var imageWidth = panelWidth * (float)Math.Max(0.2d, panel.ImageScale);
                var imageHeight = panelHeight * (float)Math.Max(0.2d, panel.ImageScale);
                var stretchedWidth = imageWidth / (float)panel.EffectiveImageCropWidthFactor;
                var stretchedHeight = imageHeight / (float)panel.EffectiveImageCropHeightFactor;
                var imageX = panelX + ((float)panel.ImageOffsetX * scale) -
                             (float)(panel.EffectiveImageCropLeft * stretchedWidth);
                var imageY = panelY + ((float)panel.ImageOffsetY * scale) -
                             (float)(panel.EffectiveImageCropTop * stretchedHeight);
                imageBounds = new Rect(imageX, imageY, stretchedWidth, stretchedHeight);
            }

            var image = new Image
            {
                Source = ImageSource.FromFile(panel.ImagePath),
                Opacity = panel.ImageOpacity <= 0 ? 0.55d : panel.ImageOpacity,
                Aspect = Aspect.Fill,
                InputTransparent = true
            };

            AbsoluteLayout.SetLayoutBounds(image, imageBounds);
            AbsoluteLayout.SetLayoutFlags(image, AbsoluteLayoutFlags.None);
            PanelImagesLayer.Children.Add(image);
        }
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
