using Microsoft.Maui.Controls.Shapes;
using WallPanelPlanner.Models;

namespace WallPanelPlanner;

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

    private async void OnAddRoomClicked(object? sender, EventArgs e)
    {
        try
        {
            await app.GymSetupViewModel.AddRoomAsync(RoomNameEntry.Text);
            RoomNameEntry.Text = string.Empty;
            SyncView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Sale", ex.Message, "OK");
        }
    }

    private void SyncView()
    {
        var viewModel = app.GymSetupViewModel;
        ActiveRoomLabel.Text = viewModel.SelectedRoom is null
            ? "Nessuna sala selezionata."
            : $"Sala selezionata: {viewModel.SelectedRoom.Name}";
        RoomsSummaryLabel.Text = viewModel.Rooms.Count == 1
            ? "1 sala"
            : $"{viewModel.Rooms.Count} sale";
        RoomNameEntry.Placeholder = viewModel.SuggestedNextRoomName;
        OpenSelectedRoomWallsButton.IsEnabled = viewModel.SelectedRoom is not null;

        RoomsHost.Children.Clear();
        RoomsEmptyLabel.IsVisible = viewModel.Rooms.Count == 0;

        foreach (var room in viewModel.Rooms.OrderBy(item => item.Name))
        {
            RoomsHost.Children.Add(BuildRoomCard(room));
        }
    }

    private View BuildRoomCard(RoomDefinition room)
    {
        var wallsCount = app.GymSetupViewModel.Walls.Count(wall => string.Equals(wall.RoomName, room.Name, StringComparison.Ordinal));
        var selectButton = new Button
        {
            Text = "Apri pareti",
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

    private async void OnOpenSelectedRoomWallsClicked(object? sender, EventArgs e)
    {
        if (app.GymSetupViewModel.SelectedRoom is null)
        {
            return;
        }

        await Shell.Current.GoToAsync("walls-page");
    }
}
