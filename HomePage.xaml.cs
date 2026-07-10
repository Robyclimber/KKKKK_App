namespace WallPanelPlanner;

public partial class HomePage : ContentPage
{
    private App? app;
    private WallPanelPlanner.Models.HomeWorkflowState currentState = WallPanelPlanner.Models.HomeWorkflowState.SetupPalestra;
    private bool isRefreshing;

    public HomePage()
    {
        try
        {
            InitializeComponent();
            app = (App)Application.Current!;
        }
        catch (Exception ex)
        {
            Title = "Errore Home";
            Content = BuildErrorView("Errore inizializzazione HomePage", ex);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (isRefreshing || app is null)
        {
            return;
        }

        try
        {
            isRefreshing = true;
            await RefreshHomeStateAsync();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async void OnGoToGymSetupClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//sala-arrampicata");
    }

    private async void OnGoToCircuitsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//circuiti");
    }

    private async void OnPrimaryActionClicked(object? sender, EventArgs e)
    {
        switch (currentState)
        {
            case WallPanelPlanner.Models.HomeWorkflowState.PrimiCircuiti:
            case WallPanelPlanner.Models.HomeWorkflowState.Operativo:
                await Shell.Current.GoToAsync("//circuiti");
                break;
            default:
                await Shell.Current.GoToAsync("//sala-arrampicata");
                break;
        }
    }

    private async void OnTrainingClicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Allenamento", "L'area allenamento resta in standby per il momento.", "OK");
    }

    private async Task RefreshHomeStateAsync()
    {
        if (app is null)
        {
            return;
        }

        var summary = await app.HomeStateService.GetSummaryAsync();
        RoomsCountLabel.Text = summary.RoomsCount.ToString();
        WallsCountLabel.Text = summary.WallsCount.ToString();
        CircuitsCountLabel.Text = summary.CircuitsCount.ToString();

        currentState = summary.WorkflowState;
        ApplyStateToView(currentState);
    }

    private void ApplyStateToView(WallPanelPlanner.Models.HomeWorkflowState state)
    {
        switch (state)
        {
            case WallPanelPlanner.Models.HomeWorkflowState.PrimiCircuiti:
                NextStepTitleLabel.Text = "Prossimo passo";
                NextStepMessageLabel.Text = "La palestra e' configurata. Ora crea il tuo primo circuito.";
                PrimaryActionButton.Text = "Vai ai circuiti";
                break;
            case WallPanelPlanner.Models.HomeWorkflowState.Operativo:
                NextStepTitleLabel.Text = "Sistema pronto";
                NextStepMessageLabel.Text = "La palestra e i circuiti sono disponibili. Puoi continuare a gestire i circuiti.";
                PrimaryActionButton.Text = "Vai ai circuiti";
                break;
            default:
                NextStepTitleLabel.Text = "Inizia da qui";
                NextStepMessageLabel.Text = "Non hai ancora configurato nessuna sala. Crea la struttura iniziale della palestra per iniziare.";
                PrimaryActionButton.Text = "Vai alla configurazione";
                break;
        }
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
