using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using WallPanelPlanner.Models;

namespace WallPanelPlanner;

public partial class WallsPage : ContentPage
{
    private readonly App app;
    private bool isRefreshing;

    public WallsPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (isRefreshing)
        {
            return;
        }

        try
        {
            isRefreshing = true;
            await app.GymSetupViewModel.LoadWallsAsync();
            SyncView();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async void OnAddWallClicked(object? sender, EventArgs e)
    {
        try
        {
            app.GymSetupViewModel.AddWall(new WallInput
            {
                Name = WallNameEntry.Text?.Trim() ?? string.Empty,
                Width = ParsePositiveDouble(WallWidthEntry.Text, "Inserisci una larghezza valida."),
                Height = ParsePositiveDouble(WallHeightEntry.Text, "Inserisci un'altezza valida.")
            });

            WallNameEntry.Text = string.Empty;
            WallWidthEntry.Text = string.Empty;
            WallHeightEntry.Text = string.Empty;
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Pareti", ex.Message, "OK");
        }
    }

    private void SyncView()
    {
        var viewModel = app.GymSetupViewModel;
        var selectedRoom = viewModel.SelectedRoom;
        var walls = viewModel.GetWallsForSelectedRoom();

        SelectedRoomLabel.Text = selectedRoom is null
            ? "Nessuna sala selezionata."
            : $"Sala selezionata: {selectedRoom.Name}";
        SelectedWallLabel.Text = viewModel.SelectedWall is null
            ? "Nessuna parete selezionata."
            : $"Parete selezionata: {viewModel.SelectedWall.Name}";
        WallsSummaryLabel.Text = walls.Count == 1 ? "1 parete" : $"{walls.Count} pareti";

        WallNameEntry.Placeholder = viewModel.SuggestedNextWallName;
        WallNameEntry.IsEnabled = selectedRoom is not null;
        WallWidthEntry.IsEnabled = selectedRoom is not null;
        WallHeightEntry.IsEnabled = selectedRoom is not null;
        OpenSelectedWallDetailButton.IsEnabled = viewModel.SelectedWall is not null;
        AddWallHintLabel.Text = selectedRoom is null
            ? "Seleziona prima una sala."
            : "Aggiungi una parete a questa sala.";
        WallsHost.Children.Clear();
        WallsEmptyLabel.IsVisible = walls.Count == 0;

        foreach (var wall in walls)
        {
            WallsHost.Children.Add(BuildWallCard(wall));
        }
    }

    private View BuildWallCard(WallDefinition wall)
    {
        var openButton = new Button
        {
            Text = "Apri dettaglio parete",
            Style = (Style)Application.Current!.Resources["PrimaryActionButtonStyle"]
        };
        openButton.Clicked += async (_, _) =>
        {
            app.GymSetupViewModel.SelectWall(wall);
            SyncView();
            await Shell.Current.GoToAsync("gym-setup-page");
        };

        return new Border
        {
            BackgroundColor = (Color)Application.Current!.Resources["PanelBlack"],
            Stroke = (Color)Application.Current.Resources["WarmGoldMuted"],
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = wall.Name,
                        FontSize = 18,
                        TextColor = (Color)Application.Current.Resources["WarmGold"],
                        FontFamily = "OpenSansSemibold"
                    },
                    new Label
                    {
                        Text = $"{wall.Width:0.#} x {wall.Height:0.#} mm",
                        TextColor = (Color)Application.Current.Resources["MutedText"]
                    },
                    new Label
                    {
                        Text = wall.Panels.Count == 1 ? "1 pannello" : $"{wall.Panels.Count} pannelli",
                        TextColor = (Color)Application.Current.Resources["MutedText"]
                    },
                    openButton
                }
            }
        };
    }

    private async void OnOpenSelectedWallDetailClicked(object? sender, EventArgs e)
    {
        if (app.GymSetupViewModel.SelectedWall is null)
        {
            return;
        }

        await Shell.Current.GoToAsync("gym-setup-page");
    }

    private static double ParsePositiveDouble(string? value, string errorMessage)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) &&
            !double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
        {
            throw new InvalidOperationException(errorMessage);
        }

        if (result <= 0)
        {
            throw new InvalidOperationException(errorMessage);
        }

        return result;
    }
}
