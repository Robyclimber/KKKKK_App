using Microsoft.Maui.Controls.Shapes;
using RouteLab.Models;

namespace RouteLab;

public partial class RoomsPage : ContentPage
{
    private readonly App app;
    private bool isRefreshing;

    public RoomsPage()
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

        using var busy = AppBusy.Show("Caricamento sale...");
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

    private void SyncView()
    {
        var viewModel = app.GymSetupViewModel;
        RoomsHost.Children.Clear();
        RoomsEmptyLabel.IsVisible = viewModel.Rooms.Count == 0;

        foreach (var room in viewModel.Rooms.OrderBy(item => item.Name))
        {
            RoomsHost.Children.Add(BuildRoomCard(room));
        }
    }

    private async void OnAddRoomClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//settings");
    }

    private View BuildRoomCard(RoomDefinition room)
    {
        var wallsCount = app.GymSetupViewModel.Walls.Count(wall => string.Equals(wall.RoomName, room.Name, StringComparison.Ordinal));
        var selectButton = new Button
        {
            Text = "Seleziona sala",
            Style = (Style)Application.Current!.Resources["PrimaryActionButtonStyle"]
        };
        selectButton.Clicked += async (_, _) =>
        {
            app.GymSetupViewModel.SelectRoom(room);
            SyncView();
            await Shell.Current.GoToAsync("walls-page");
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
                        Text = room.Name,
                        FontSize = 18,
                        TextColor = (Color)Application.Current.Resources["WarmGold"],
                        FontFamily = "OpenSansSemibold"
                    },
                    new Label
                    {
                        Text = wallsCount == 1 ? "1 parete" : $"{wallsCount} pareti",
                        TextColor = (Color)Application.Current.Resources["MutedText"]
                    },
                    selectButton
                }
            }
        };
    }

}
