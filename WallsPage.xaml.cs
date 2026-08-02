using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using RouteLab.Models;

namespace RouteLab;

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

        using var busy = AppBusy.Show("Caricamento pareti...");
        try
        {
            isRefreshing = true;
            await app.GymSetupViewModel.EnsureLoadedAsync();
            SyncView();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async void OnNewWallClicked(object? sender, EventArgs e)
    {
        if (app.GymSetupViewModel.SelectedRoom is null)
        {
            await DisplayAlertAsync("Nuova parete", "Seleziona prima una sala.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("new-wall-page");
    }

    private void SyncView()
    {
        var viewModel = app.GymSetupViewModel;
        var selectedRoom = viewModel.SelectedRoom;
        var walls = viewModel.GetWallsForSelectedRoom();

        SelectedRoomLabel.Text = selectedRoom is null
            ? "Nessuna sala selezionata."
            : $"Sala selezionata: {selectedRoom.Name}";
        WallsSummaryLabel.Text = walls.Count == 1 ? "1 parete" : $"{walls.Count} pareti";

        WallsActions.CanAdd = selectedRoom is not null;
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
            Text = "Apri parete",
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

}
