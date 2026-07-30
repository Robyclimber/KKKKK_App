using System.Globalization;
using RouteLab.Models;

namespace RouteLab;

public partial class NewWallPage : ContentPage
{
    private readonly App app;
    private bool isSaving;

    public NewWallPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        using var busy = AppBusy.Show("Preparazione nuova parete...");
        await app.GymSetupViewModel.EnsureLoadedAsync();
        var viewModel = app.GymSetupViewModel;
        var selectedRoom = viewModel.SelectedRoom;

        RoomContextLabel.Text = selectedRoom is null
            ? "Nessuna sala selezionata."
            : $"Sala: {selectedRoom.Name}";
        WallNameEntry.Placeholder = viewModel.SuggestedNextWallName;
        WallActions.CanSave = selectedRoom is not null;
        SetEditorEnabled(selectedRoom is not null);
    }

    private async void OnSaveWallClicked(object? sender, EventArgs e)
    {
        if (isSaving)
        {
            return;
        }

        using var busy = AppBusy.Show("Salvataggio parete...");
        try
        {
            isSaving = true;
            WallActions.CanSave = false;

            var viewModel = app.GymSetupViewModel;
            viewModel.AddWall(new WallInput
            {
                Name = WallNameEntry.Text?.Trim() ?? string.Empty,
                Width = ParsePositiveDouble(WallWidthEntry.Text, "Inserisci una larghezza valida."),
                Height = ParsePositiveDouble(WallHeightEntry.Text, "Inserisci un'altezza valida.")
            });

            await viewModel.SaveSelectedWallAsync();
            await Shell.Current.GoToAsync("..");
            await Shell.Current.GoToAsync("gym-setup-page");
        }
        catch (InvalidOperationException ex)
        {
            StatusLabel.Text = ex.Message;
            await DisplayAlertAsync("Nuova parete", ex.Message, "OK");
        }
        finally
        {
            isSaving = false;
            WallActions.CanSave = app.GymSetupViewModel.SelectedRoom is not null;
        }
    }

    private void SetEditorEnabled(bool isEnabled)
    {
        WallNameEntry.IsEnabled = isEnabled;
        WallWidthEntry.IsEnabled = isEnabled;
        WallHeightEntry.IsEnabled = isEnabled;
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
