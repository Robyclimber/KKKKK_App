namespace RouteLab.Models;

public sealed class GymSetupPageState
{
    public string WorkflowTitleText { get; init; } = "Configurazione palestra";

    public string WorkflowMessageText { get; init; } = "Inizia creando una sala.";

    public string ActiveRoomText { get; init; } = "Nessuna sala selezionata.";

    public string ActiveWallText { get; init; } = "Nessuna parete selezionata.";

    public string ActivePanelText { get; init; } = "Nessun pannello selezionato.";

    public string NextActionText { get; init; } = "Crea una sala per iniziare.";

    public RoomDefinition? SelectedRoom { get; init; }

    public IReadOnlyList<WallDefinition> VisibleWalls { get; init; } = Array.Empty<WallDefinition>();

    public WallDefinition? SelectedWall { get; init; }

    public bool HasRooms { get; init; }

    public bool HasVisibleWalls { get; init; }

    public bool CanAddWall { get; init; }

    public bool CanEditPanels { get; init; }

    public bool CanManageWallImage { get; init; }

    public bool CanSaveWall { get; init; }

    public string RoomSummaryText { get; init; } = "Nessuna sala presente.";

    public string WallSelectionHintText { get; init; } = "Crea una sala per iniziare a configurare le pareti.";

    public string WallInfoText { get; init; } = "Nessuna parete selezionata.";

    public string PanelEditorModeText { get; init; } = "Inserimento nuovo pannello";

    public string SelectedPanelSummaryText { get; init; } = "Nessun pannello selezionato.";

    public bool ShowEmptyPanels { get; init; }

    public string WallImageInfoText { get; init; } = "Nessuna immagine associata.";

    public string WallImageOffsetXText { get; init; } = "0";

    public string WallImageOffsetYText { get; init; } = "0";

    public double WallImageScale { get; init; } = 1d;

    public double WallImageOpacity { get; init; } = 0.55d;

    public string WallImageCropLeftText { get; init; } = "0";

    public string WallImageCropTopText { get; init; } = "0";

    public string WallImageCropRightText { get; init; } = "0";

    public string WallImageCropBottomText { get; init; } = "0";
}

