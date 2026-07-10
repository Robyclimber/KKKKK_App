namespace WallPanelPlanner;

public partial class UtilityPage : ContentPage
{
    private bool isResetting;

    public UtilityPage()
    {
        InitializeComponent();
    }

    private async void OnResetDatabaseClicked(object? sender, EventArgs e)
    {
        if (isResetting)
        {
            return;
        }

        var confirm = await DisplayAlertAsync(
            "Conferma reset",
            "Questo resetta completamente il database dell'app. Vuoi continuare?",
            "Si, resetta",
            "Annulla");

        if (!confirm)
        {
            return;
        }

        try
        {
            isResetting = true;
            ResetStatusLabel.Text = "Reset in corso...";

            var app = (App)Application.Current!;
            await app.SqliteDatabaseFactory.ResetAllDataAsync();
            await app.GymSetupViewModel.LoadWallsAsync();
            await app.CircuitEditorViewModel.LoadCircuitsAsync();

            ResetStatusLabel.Text = "Database resettato correttamente.";
            await DisplayAlertAsync("Utility", "Database resettato. Puoi ricreare tutto da zero.", "OK");
        }
        catch (Exception ex)
        {
            ResetStatusLabel.Text = "Errore durante il reset.";
            await DisplayAlertAsync("Utility", $"Errore reset database: {ex.Message}", "OK");
        }
        finally
        {
            isResetting = false;
        }
    }
}
