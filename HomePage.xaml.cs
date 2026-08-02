namespace RouteLab;

public partial class HomePage : ContentPage
{
    private App? app;
    private RouteLab.Models.HomeWorkflowState currentState = RouteLab.Models.HomeWorkflowState.SetupPalestra;
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

        using var busy = AppBusy.Show("Aggiornamento home...");
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
        await Shell.Current.GoToAsync("//sale");
    }

    private async void OnGoToCircuitsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//circuiti");
    }

    private async void OnPrimaryActionClicked(object? sender, EventArgs e)
    {
        switch (currentState)
        {
            case RouteLab.Models.HomeWorkflowState.PrimiCircuiti:
            case RouteLab.Models.HomeWorkflowState.Operativo:
                await Shell.Current.GoToAsync("//circuiti");
                break;
            default:
                await Shell.Current.GoToAsync("//sale");
                break;
        }
    }

    private async void OnTrainingClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//allenamento");
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

    private void ApplyStateToView(RouteLab.Models.HomeWorkflowState state)
    {
        switch (state)
        {
            case RouteLab.Models.HomeWorkflowState.PrimiCircuiti:
                NextStepTitleLabel.Text = "Prossimo passo";
                NextStepMessageLabel.Text = "La palestra e' configurata. Ora crea il tuo primo circuito.";
                PrimaryActionButton.Text = "Vai ai circuiti";
                break;
            case RouteLab.Models.HomeWorkflowState.Operativo:
                NextStepTitleLabel.Text = "Sistema pronto";
                NextStepMessageLabel.Text = "La palestra e i circuiti sono disponibili. Puoi continuare a gestire i circuiti.";
                PrimaryActionButton.Text = "Vai ai circuiti";
                break;
            default:
                NextStepTitleLabel.Text = "Inizia da qui";
                NextStepMessageLabel.Text = "Non hai ancora configurato nessuna sala. Crea la struttura iniziale della palestra per iniziare.";
                PrimaryActionButton.Text = "Vai a sale e pareti";
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
