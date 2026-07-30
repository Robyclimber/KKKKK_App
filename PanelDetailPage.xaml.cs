using System.Globalization;
using RouteLab.Models;
using RouteLab.Services;
using RouteLab.ViewModels;

namespace RouteLab;

public partial class PanelDetailPage : ContentPage
{
    private readonly GymSetupViewModel viewModel;
    private readonly IGymSetupEditorStateService editorStateService;

    public PanelDetailPage()
    {
        InitializeComponent();
        var app = (App)Application.Current!;
        viewModel = app.GymSetupViewModel;
        editorStateService = app.GymSetupEditorStateService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadEditor();
    }

    private void LoadEditor()
    {
        var panel = viewModel.SelectedPanel;
        var state = editorStateService.BuildPanelEditor(viewModel, useSelectedPanelValues: panel is not null);

        PanelNameEntry.Text = state.PanelNameText;
        PanelXEntry.Text = state.PanelXText;
        PanelYEntry.Text = state.PanelYText;
        PanelWidthEntry.Text = state.PanelWidthText;
        PanelHeightEntry.Text = state.PanelHeightText;
        HoleOffsetEntry.Text = state.HoleOffsetText;
        HoleOffsetYEntry.Text = state.HoleOffsetYText;
        HoleHorizontalEntry.Text = state.HoleHorizontalText;
        HoleVerticalEntry.Text = state.HoleVerticalText;

        PageHeadingLabel.Text = panel is null ? "Nuovo pannello" : panel.Name;
        PanelContextLabel.Text = viewModel.SelectedWall is null
            ? "Nessuna parete selezionata."
            : $"{viewModel.SelectedRoom?.Name} / {viewModel.SelectedWall.Name}";
        Title = panel is null ? "Nuovo pannello" : $"Pannello - {panel.Name}";
        PanelActions.CanAdd = viewModel.HasSelectedWall;
        PanelActions.CanSave = viewModel.HasSelectedWall;
        PanelActions.CanDelete = panel is not null;
        OpenHoleGridEditorButton.IsEnabled = panel is not null;
        ManageImageButton.IsEnabled = panel is not null;
        CropImageButton.IsEnabled = panel is not null &&
                                    !string.IsNullOrWhiteSpace(panel.ImagePath) &&
                                    File.Exists(panel.ImagePath);
        MapHoldsButton.IsEnabled = CropImageButton.IsEnabled;
        SyncImagePreview(panel);
    }

    private void SyncImagePreview(PanelDefinition? panel)
    {
        var hasImage = panel is not null &&
                       !string.IsNullOrWhiteSpace(panel.ImagePath) &&
                       File.Exists(panel.ImagePath);

        PanelImagePreviewFrame.IsVisible = hasImage;
        PanelImagePreview.Source = hasImage ? ImageSource.FromFile(panel!.ImagePath) : null;
        PanelImageStatusLabel.Text = panel is null
            ? "Salva prima il pannello per associargli una foto."
            : panel.IsImageRectified && hasImage
                ? "Immagine rettificata pronta e adattata al pannello."
                : hasImage
                    ? "Foto caricata. Esegui ritaglio e adattamento."
                    : "Nessuna foto associata.";

        if (hasImage && panel!.Width > 0d && panel.Height > 0d)
        {
            var previewWidth = Math.Max(240d, Width - 72d);
            PanelImagePreviewFrame.HeightRequest = Math.Clamp(
                previewWidth * panel.Height / panel.Width,
                140d,
                360d);
        }
    }

    private async void OnSavePanelClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Salvataggio pannello...");
        try
        {
            if (!viewModel.HasSelectedWall)
            {
                throw new InvalidOperationException("Seleziona prima una parete.");
            }

            var input = ReadPanelInput();
            if (viewModel.SelectedPanel is null)
            {
                viewModel.AddPanel(input);
            }
            else
            {
                viewModel.UpdateSelectedPanel(input);
            }

            await viewModel.SaveSelectedWallAsync();
            StatusLabel.Text = "Pannello salvato.";
            LoadEditor();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Pannello", ex.Message, "OK");
        }
    }

    private async void OnNewPanelClicked(object? sender, EventArgs e)
    {
        if (!viewModel.HasSelectedWall)
        {
            await DisplayAlertAsync("Nuovo pannello", "Seleziona prima una parete.", "OK");
            return;
        }

        viewModel.ClearSelectedPanel();
        LoadEditor();
        await PanelDetailScrollView.ScrollToAsync(0, 0, true);
    }

    private async void OnManageImageClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            await DisplayAlertAsync("Pannello", "Salva prima il pannello.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("panel-image-page");
    }

    private async void OnOpenHoleGridEditorClicked(object? sender, EventArgs e)
    {
        if (viewModel.SelectedPanel is null)
        {
            await DisplayAlertAsync("Griglia fori", "Salva prima il pannello.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("panel-hole-grid-editor-page");
    }

    private async void OnCropImageClicked(object? sender, EventArgs e)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
        {
            await DisplayAlertAsync("Pannello", "Carica prima una foto del pannello.", "OK");
            return;
        }

        await Navigation.PushAsync(new PanelCropEditorPage());
    }

    private async void OnMapHoldsClicked(object? sender, EventArgs e)
    {
        var wall = viewModel.SelectedWall;
        var panel = viewModel.SelectedPanel;
        if (wall is null || panel is null)
        {
            await DisplayAlertAsync("Prese del pannello", "Seleziona prima un pannello.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
        {
            await DisplayAlertAsync("Prese del pannello", "Carica prima una foto del pannello.", "OK");
            return;
        }

        var app = (App)Application.Current!;
        await Navigation.PushAsync(new HoldAnalysisPage(
            app.WallConfigurationStorageService,
            wall,
            panel));
    }

    private async void OnDeletePanelClicked(object? sender, EventArgs e)
    {
        var panel = viewModel.SelectedPanel;
        if (panel is null)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            "Elimina pannello",
            $"Vuoi eliminare il pannello {panel.Name}?",
            "Elimina",
            "Annulla");
        if (!confirmed)
        {
            return;
        }

        using var busy = AppBusy.Show("Eliminazione pannello...");
        viewModel.DeleteSelectedPanel();
        await viewModel.SaveSelectedWallAsync();
        await Shell.Current.GoToAsync("..");
    }

    private PanelInput ReadPanelInput()
    {
        var currentPanel = viewModel.SelectedPanel;
        return new PanelInput
        {
            Name = PanelNameEntry.Text?.Trim() ?? string.Empty,
            X = ParseNonNegativeDouble(PanelXEntry.Text),
            Y = ParseNonNegativeDouble(PanelYEntry.Text),
            Width = ParsePositiveDouble(PanelWidthEntry.Text),
            Height = ParsePositiveDouble(PanelHeightEntry.Text),
            EdgeOffsetX = ParseNonNegativeDouble(HoleOffsetEntry.Text),
            EdgeOffsetY = ParseNonNegativeDouble(HoleOffsetYEntry.Text),
            HorizontalSpacing = ParsePositiveDouble(HoleHorizontalEntry.Text),
            VerticalSpacing = ParsePositiveDouble(HoleVerticalEntry.Text),
            LedRoutingAxis = currentPanel?.LedRoutingAxis ?? LedRoutingAxis.Vertical,
            LedStartDirection = currentPanel?.LedStartDirection ??
                                viewModel.SelectedWall?.LedVerticalDirection ??
                                LedStartDirection.TopToBottom
        };
    }

    private static double ParsePositiveDouble(string? text)
    {
        var value = ParseDouble(text);
        if (value <= 0d)
        {
            throw new InvalidOperationException("Inserisci valori positivi per dimensioni e spaziatura.");
        }

        return value;
    }

    private static double ParseNonNegativeDouble(string? text)
    {
        var value = ParseDouble(text);
        if (value < 0d)
        {
            throw new InvalidOperationException("Posizioni e offset non possono essere negativi.");
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
}
