using System.Globalization;
using RouteLab.Drawing;
using RouteLab.Models;

namespace RouteLab;

public partial class NextHoldSuggestionPage : ContentPage, IQueryAttributable
{
    private readonly App app;
    private readonly CircuitEditorDrawable previewDrawable = new();
    private CircuitDefinition? circuit;
    private WallDefinition? wall;
    private NextHoldSuggestionResult? suggestion;
    private string? pendingCircuitId;
    private string? pendingWallName;
    private bool isRefreshing;
    private double previewZoom = 1d;
    private double basePreviewWidth = 320d;
    private double basePreviewHeight = 320d;

    public NextHoldSuggestionPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
        PreviewCanvas.Drawable = previewDrawable;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        pendingCircuitId = ReadQueryValue(query, "circuitId");
        pendingWallName = ReadQueryValue(query, "wallName");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (isRefreshing)
        {
            return;
        }

        using var busy = AppBusy.Show("Preparazione suggerimento...");
        try
        {
            isRefreshing = true;
            await app.CircuitEditorViewModel.LoadCircuitsAsync();
            circuit = ResolveCircuit();
            if (circuit is null)
            {
                throw new InvalidOperationException("Il circuito richiesto non e' disponibile.");
            }

            app.CircuitEditorViewModel.SelectCircuit(circuit);
            var requestedWallName = string.IsNullOrWhiteSpace(pendingWallName)
                ? circuit.Movements
                      .Where(movement => !movement.IsFootHold)
                      .OrderBy(movement => movement.Sequence)
                      .LastOrDefault()?.WallName
                  ?? circuit.GetWallNames().FirstOrDefault()
                : Uri.UnescapeDataString(pendingWallName);
            wall = app.CircuitEditorViewModel.GetWallsForCircuit(circuit)
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, requestedWallName, StringComparison.Ordinal));
            if (wall is null)
            {
                throw new InvalidOperationException("La parete associata al circuito non e' disponibile.");
            }
            app.CircuitEditorViewModel.SetActiveWall(wall);

            RefreshProfilePicker();
            RefreshView();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Presa successiva", ex.Message, "OK");
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async void OnProfileChanged(object? sender, EventArgs e)
    {
        if (isRefreshing || circuit is null || ProfilePicker.SelectedItem is not ClimberProfileDefinition profile)
        {
            return;
        }

        try
        {
            circuit.ClimberProfileId = profile.Id;
            await app.CircuitRepository.SaveAsync(circuit);
            suggestion = null;
            RefreshView();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Profilo biomeccanico", ex.Message, "OK");
        }
    }

    private async void OnCalculateClicked(object? sender, EventArgs e)
    {
        if (circuit is null || wall is null)
        {
            return;
        }

        var leftHand = ResolveCurrentHandStateHole(HandSide.Left);
        var rightHand = ResolveCurrentHandStateHole(HandSide.Right);
        if (leftHand is null || rightHand is null)
        {
            var message = circuit.GetWallNames().Count > 1
                ? $"Per calcolare il movimento sulla parete {wall.Name}, entrambe le mani devono avere una posizione corrente su questa parete. Le transizioni geometriche tra pareti richiedono una futura mappatura della loro posizione relativa."
                : "Nel circuito servono almeno una posizione corrente per la mano SX e una per la mano DX.";
            await DisplayAlertAsync(
                "Presa successiva",
                message,
                "OK");
            return;
        }

        using var busy = AppBusy.Show("Calcolo presa successiva...");
        await Task.Yield();
        try
        {
            var settings = app.AppSettingsService.Load();
            var profile = settings.ResolveClimberProfile(circuit.ClimberProfileId);
            suggestion = app.NextHoldSuggestionService.SuggestNextHold(new NextHoldSuggestionRequest
            {
                Wall = wall,
                Circuit = circuit,
                ClimberProfile = profile,
                MovingHand = DetermineNextMovingHand(),
                CurrentLeftHandHoleNumber = leftHand.Value.Number,
                CurrentRightHandHoleNumber = rightHand.Value.Number,
                CurrentFootHoleNumbers = ResolveFootHoles().Select(hole => hole.Number).ToList(),
                MaxSuggestions = 3
            });

            if (suggestion.SuggestedHoleNumber is null)
            {
                await DisplayAlertAsync(
                    "Presa successiva",
                    "Nessuna presa compatibile trovata con la posizione corrente.",
                    "OK");
            }

            RefreshView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Presa successiva", ex.Message, "OK");
        }
    }

    private async void OnApplyClicked(object? sender, EventArgs e)
    {
        var suggestedHole = ResolveSuggestedHole();
        if (circuit is null || wall is null || suggestedHole is null)
        {
            await DisplayAlertAsync("Presa successiva", "Calcola prima una presa valida.", "OK");
            return;
        }

        using var busy = AppBusy.Show("Applicazione suggerimento...");
        try
        {
            var movingHand = DetermineNextMovingHand();
            app.CircuitEditingService.ToggleMovement(
                circuit,
                wall.Name,
                suggestedHole.Value,
                movingHand,
                MovementRole.Normal);
            await app.CircuitRepository.SaveAsync(circuit);
            suggestion = null;
            RefreshView();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Presa successiva", ex.Message, "OK");
        }
    }

    private void RefreshProfilePicker()
    {
        var settings = app.AppSettingsService.Load();
        var profiles = settings.ClimberProfiles.Select(profile => profile.Clone()).ToList();
        isRefreshing = true;
        ProfilePicker.ItemsSource = profiles;
        ProfilePicker.SelectedItem = profiles.FirstOrDefault(profile =>
                                         string.Equals(profile.Id, circuit?.ClimberProfileId, StringComparison.OrdinalIgnoreCase))
                                     ?? profiles.First(profile => profile.IsDefault);
        isRefreshing = false;
    }

    private void RefreshView()
    {
        var leftHand = ResolveCurrentHandStateHole(HandSide.Left);
        var rightHand = ResolveCurrentHandStateHole(HandSide.Right);
        var footHoles = ResolveFootHoles();
        var footSummary = footHoles.Count == 0
            ? "-"
            : string.Join(", ", footHoles.Select(hole => hole.Number));

        CircuitContextLabel.Text = circuit is null || wall is null
            ? "Circuito non disponibile."
            : $"Circuito: {circuit.Name} | Parete: {wall.Name} | Grado: {circuit.Difficulty} | Inclinazione: {circuit.Inclination}";
        CurrentStateLabel.Text =
            $"Prossima mano: {(DetermineNextMovingHand() == HandSide.Right ? "DX" : "SX")} | " +
            $"Mani: SX {FormatHole(leftHand)}, DX {FormatHole(rightHand)} | Piedi: {footSummary}";

        var suggestedHole = ResolveSuggestedHole();
        ResultLabel.Text = suggestion is null || suggestedHole is null
            ? "Nessun suggerimento calcolato."
            : $"{BuildMovementInstruction(suggestion, suggestedHole.Value, DetermineNextMovingHand())} " +
              $"{Environment.NewLine}{Environment.NewLine}{BuildMovementPlanSummary(suggestion.MovementPlan)}" +
              $"{Environment.NewLine}{Environment.NewLine}{suggestion.PrimaryReason}. {suggestion.SecondaryReason}. " +
              $"{BuildFootSupportSummary(suggestion)}";
        CalculateButton.IsEnabled = circuit is not null && wall is not null && leftHand is not null && rightHand is not null;
        ApplyButton.IsEnabled = suggestedHole is not null;

        var previewFootHoles = ResolveSuggestedFootHoles(footHoles);
        previewDrawable.Wall = wall;
        previewDrawable.Circuit = circuit;
        previewDrawable.HighlightedHole = null;
        previewDrawable.SelectedHoles = new[] { leftHand, rightHand }
            .Where(hole => hole.HasValue)
            .Select(hole => hole!.Value)
            .Concat(previewFootHoles)
            .GroupBy(hole => hole.Number)
            .Select(group => group.First())
            .ToList();
        previewDrawable.SuggestedHole = suggestedHole;
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
    }

    private CircuitDefinition? ResolveCircuit()
    {
        if (int.TryParse(pendingCircuitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var circuitId))
        {
            return app.CircuitEditorViewModel.Circuits.FirstOrDefault(item => item.Id == circuitId);
        }

        return app.CircuitEditorViewModel.SelectedCircuit;
    }

    private WallHoleDefinition? ResolveCurrentHandStateHole(HandSide hand)
    {
        if (circuit is null || wall is null)
        {
            return null;
        }

        var holes = wall.GetOrderedHoles();
        var movement = circuit.Movements
            .Where(item => item.Hand == hand && item.Role != MovementRole.Top && !item.IsFootHold)
            .OrderBy(item => item.Sequence)
            .LastOrDefault()
            ?? circuit.Movements
                .Where(item =>
                    string.Equals(item.WallName, wall.Name, StringComparison.Ordinal) &&
                    item.Hand == hand &&
                    item.Role == MovementRole.Start)
                .OrderBy(item => item.Sequence)
                .FirstOrDefault();
        if (movement is null ||
            !string.Equals(movement.WallName, wall.Name, StringComparison.Ordinal))
        {
            return null;
        }

        var hole = holes.FirstOrDefault(item => item.Number == movement.HoleNumber);
        return hole.Number == 0 ? null : hole;
    }

    private IReadOnlyList<WallHoleDefinition> ResolveFootHoles()
    {
        if (circuit is null || wall is null)
        {
            return Array.Empty<WallHoleDefinition>();
        }

        var holesByNumber = wall.GetOrderedHoles().ToDictionary(hole => hole.Number);
        return circuit.Movements
            .Where(movement =>
                movement.IsFootHold &&
                string.Equals(movement.WallName, wall.Name, StringComparison.Ordinal))
            .OrderBy(movement => movement.HoleNumber)
            .Select(movement => holesByNumber.GetValueOrDefault(movement.HoleNumber))
            .Where(hole => hole.Number > 0)
            .ToList();
    }

    private WallHoleDefinition? ResolveSuggestedHole()
    {
        if (wall is null || suggestion?.SuggestedHoleNumber is null)
        {
            return null;
        }

        var hole = wall.GetOrderedHoles()
            .FirstOrDefault(item => item.Number == suggestion.SuggestedHoleNumber.Value);
        return hole.Number == 0 ? null : hole;
    }

    private IReadOnlyList<WallHoleDefinition> ResolveSuggestedFootHoles(
        IReadOnlyList<WallHoleDefinition> availableFootHoles)
    {
        if (suggestion?.SupportFootHoleNumbers.Count > 0 && wall is not null)
        {
            var supportNumbers = suggestion.SupportFootHoleNumbers.ToHashSet();
            return wall.GetOrderedHoles()
                .Where(hole => supportNumbers.Contains(hole.Number))
                .ToList();
        }

        return availableFootHoles;
    }

    private HandSide DetermineNextMovingHand()
    {
        var lastMovement = circuit?.Movements
            .Where(movement => movement.Role == MovementRole.Normal)
            .OrderBy(movement => movement.Sequence)
            .LastOrDefault();
        return lastMovement is null || lastMovement.Hand == HandSide.Left
            ? HandSide.Right
            : HandSide.Left;
    }

    private static string BuildFootSupportSummary(NextHoldSuggestionResult result)
    {
        if (result.SupportFootHoleNumbers.Count < 2)
        {
            return $"Triangolo di appoggio non disponibile; affidabilita {result.CenterConfidenceLabel}";
        }

        var feet = string.Join(", ", result.SupportFootHoleNumbers);
        var center = result.CenterInsideSupportTriangle
            ? "baricentro interno"
            : $"baricentro distante {result.DistanceFromSupportTriangle:0} mm";
        return $"Piedi {feet}, {center}, baricentro ({result.BiomechanicalCenterX:0}, {result.BiomechanicalCenterY:0}) mm, " +
               $"momento {result.GravityTorqueNewtonMeter:0.0} Nm";
    }

    private static string BuildMovementInstruction(
        NextHoldSuggestionResult result,
        WallHoleDefinition suggestedHole,
        HandSide movingHand)
    {
        var hand = movingHand == HandSide.Right ? "DX" : "SX";
        if (result.FootMoveHoleNumbers.Count == 0)
        {
            var supports = result.SupportFootHoleNumbers.Count == 0
                ? "correnti"
                : string.Join(", ", result.SupportFootHoleNumbers);
            return $"Mantieni i piedi sugli appoggi {supports}, poi porta la mano {hand} al foro {suggestedHole.Number}.";
        }

        var feetToMove = string.Join(", ", result.FootMoveHoleNumbers);
        var finalSupports = string.Join(", ", result.SupportFootHoleNumbers);
        return result.FootMoveHoleNumbers.Count == 1
            ? $"Prima sposta un piede sul foro {feetToMove} (appoggi finali {finalSupports}), " +
              $"poi porta la mano {hand} al foro {suggestedHole.Number}."
            : $"Prima riposiziona i piedi sui fori {feetToMove} (appoggi finali {finalSupports}), " +
              $"poi porta la mano {hand} al foro {suggestedHole.Number}.";
    }

    private static string BuildMovementPlanSummary(ClimbingMovementPlan plan)
    {
        var complementaryMovements = plan.MovementTypes
            .Where(type => type != plan.PrimaryMovement)
            .Select(GetMovementTypeLabel)
            .ToList();
        var movementSummary = $"Tecnica statica: {GetMovementTypeLabel(plan.PrimaryMovement)}";
        if (complementaryMovements.Count > 0)
        {
            movementSummary += $" | Componenti: {string.Join(", ", complementaryMovements)}";
        }

        var balanceSummary = plan.BalanceTechniques.Count == 0
            ? "Equilibrio: gestione diretta degli appoggi"
            : $"Equilibrio: {string.Join(", ", plan.BalanceTechniques.Select(GetBalanceTechniqueLabel))}";
        var steps = plan.Steps.Count == 0
            ? "Sequenza non disponibile"
            : $"Sequenza:{Environment.NewLine}{string.Join(
                Environment.NewLine,
                plan.Steps.Select(step => $"{step.Sequence}. {BuildMovementStepLabel(step)}"))}";

        return $"{movementSummary}{Environment.NewLine}{balanceSummary}{Environment.NewLine}{steps}";
    }

    private static string BuildMovementStepLabel(ClimbingMovementStep step)
    {
        if (step.Action == ClimbingMovementAction.MaintainContact)
        {
            var bodyPart = step.BodyPart switch
            {
                ClimbingBodyPart.LeftHand => "la mano SX",
                ClimbingBodyPart.RightHand => "la mano DX",
                _ => "un piede"
            };
            return $"Mantieni {bodyPart} sul foro {step.ToHoleNumber}.";
        }

        if (step.Action == ClimbingMovementAction.Move &&
            step.BodyPart == ClimbingBodyPart.Feet)
        {
            return $"Sposta un piede dal foro {step.FromHoleNumber} al foro {step.ToHoleNumber}.";
        }

        if (step.Action == ClimbingMovementAction.Load)
        {
            return $"Carica progressivamente il piede sul foro {step.ToHoleNumber}.";
        }

        if (step.Action == ClimbingMovementAction.TransferWeight)
        {
            return "Trasferisci progressivamente il peso mantenendo ferme le mani.";
        }

        if (step.Action == ClimbingMovementAction.Move)
        {
            var hand = step.BodyPart == ClimbingBodyPart.LeftHand ? "SX" : "DX";
            return $"Sposta la mano {hand} dal foro {step.FromHoleNumber} al foro {step.ToHoleNumber}.";
        }

        return "Stabilizza la postura sulla nuova configurazione.";
    }

    private static string GetMovementTypeLabel(ClimbingMovementType type)
    {
        return type switch
        {
            ClimbingMovementType.Lateral => "laterale",
            ClimbingMovementType.HipRotation => "rotazione del bacino",
            ClimbingMovementType.WeightTransfer => "trasferimento del peso",
            ClimbingMovementType.RockOver => "rock-over",
            _ => "frontale"
        };
    }

    private static string GetBalanceTechniqueLabel(ClimbingBalanceTechnique technique)
    {
        return technique switch
        {
            ClimbingBalanceTechnique.StableSupportTriangle => "triangolo di appoggio stabile",
            ClimbingBalanceTechnique.ProgressiveWeightTransfer => "trasferimento progressivo",
            ClimbingBalanceTechnique.Counterbalance => "controbilanciamento",
            ClimbingBalanceTechnique.FootLoading => "carico sul piede",
            ClimbingBalanceTechnique.Flagging => "bandiera",
            _ => "appoggi"
        };
    }

    private static string FormatHole(WallHoleDefinition? hole)
    {
        return hole?.Number.ToString(CultureInfo.InvariantCulture) ?? "-";
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom + 0.25d, 1d, 4d);
        UpdatePreviewZoomLayout();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        previewZoom = Math.Clamp(previewZoom - 0.25d, 1d, 4d);
        UpdatePreviewZoomLayout();
    }

    private void OnZoomResetClicked(object? sender, EventArgs e)
    {
        previewZoom = 1d;
        UpdatePreviewZoomLayout();
    }

    private void OnPreviewViewportSizeChanged(object? sender, EventArgs e)
    {
        if (PreviewViewport.Width <= 0d || PreviewViewport.Height <= 0d)
        {
            return;
        }

        basePreviewWidth = Math.Max(280d, PreviewViewport.Width - 4d);
        basePreviewHeight = Math.Max(280d, PreviewViewport.Height - 4d);
        UpdatePreviewBaseScale();
        UpdatePreviewZoomLayout();
    }

    private void UpdatePreviewBaseScale()
    {
        if (wall is null || wall.Width <= 0d || wall.Height <= 0d)
        {
            previewDrawable.PixelsPerMillimeter = 0.1f;
            return;
        }

        var widthScale = Math.Max(0.01d, (basePreviewWidth - 48d) / wall.Width);
        var heightScale = Math.Max(0.01d, (basePreviewHeight - 48d) / wall.Height);
        previewDrawable.PixelsPerMillimeter = (float)Math.Min(widthScale, heightScale);
    }

    private void UpdatePreviewZoomLayout()
    {
        previewDrawable.ZoomFactor = (float)previewZoom;
        var desiredSize = previewDrawable.GetDesiredSize(previewZoom);
        PreviewCanvas.WidthRequest = Math.Max(basePreviewWidth, desiredSize.Width);
        PreviewCanvas.HeightRequest = Math.Max(basePreviewHeight, desiredSize.Height);
        PreviewCanvas.Invalidate();
    }

    private static string? ReadQueryValue(IDictionary<string, object> query, string key)
    {
        if (!query.TryGetValue(key, out var value))
        {
            return null;
        }

        return Uri.UnescapeDataString(value?.ToString() ?? string.Empty);
    }
}
