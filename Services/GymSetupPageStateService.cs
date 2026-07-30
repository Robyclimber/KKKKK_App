using RuoteLab.Models;
using RuoteLab.ViewModels;

namespace RuoteLab.Services;

public sealed class GymSetupPageStateService : IGymSetupPageStateService
{
    private readonly IGymSetupEditorStateService editorStateService;

    public GymSetupPageStateService(IGymSetupEditorStateService editorStateService)
    {
        this.editorStateService = editorStateService;
    }

    public GymSetupPageState Build(GymSetupViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var selectedWall = viewModel.SelectedWall;
        var selectedRoom = viewModel.SelectedRoom;
        var visibleWalls = viewModel.GetWallsForSelectedRoom();
        var roomCount = viewModel.Rooms.Count;
        var wallCount = visibleWalls.Count;
        var panelCount = selectedWall?.Panels.Count ?? 0;
        var selectedPanel = viewModel.SelectedPanel;
        var panelHasImage = selectedPanel is not null && !string.IsNullOrWhiteSpace(selectedPanel.ImagePath);
        var panelEditorState = editorStateService.BuildPanelEditor(viewModel, useSelectedPanelValues: true);
        var workflow = BuildWorkflowState(selectedRoom, selectedWall, roomCount, wallCount, panelCount);

        return new GymSetupPageState
        {
            WorkflowTitleText = workflow.Title,
            WorkflowMessageText = workflow.Message,
            ActiveRoomText = selectedRoom is null
                ? "Nessuna sala selezionata."
                : $"Sala selezionata: {selectedRoom.Name}",
            ActiveWallText = selectedWall is null
                ? "Nessuna parete selezionata."
                : $"Parete selezionata: {selectedWall.Name} - {selectedWall.Width:0.#} x {selectedWall.Height:0.#} mm",
            ActivePanelText = selectedPanel is null
                ? "Nessun pannello selezionato."
                : $"Pannello selezionato: {selectedPanel.Name} - {selectedPanel.Width:0.#} x {selectedPanel.Height:0.#} mm - Fori: {selectedPanel.HoleCount}",
            NextActionText = workflow.NextAction,
            SelectedRoom = selectedRoom,
            VisibleWalls = visibleWalls,
            SelectedWall = selectedWall,
            HasRooms = roomCount > 0,
            HasVisibleWalls = wallCount > 0,
            CanAddWall = selectedRoom is not null,
            CanEditPanels = selectedWall is not null,
            CanManageWallImage = selectedPanel is not null,
            CanSaveWall = selectedWall is not null,
            RoomSummaryText = selectedRoom is null
                ? $"Sale presenti: {roomCount}"
                : $"Sala selezionata: {selectedRoom.Name} - Pareti: {wallCount}",
            WallSelectionHintText = wallCount == 0
                ? (selectedRoom is null
                    ? "Crea o seleziona prima una sala."
                    : $"Nessuna parete nella sala {selectedRoom.Name}. Aggiungine una per continuare.")
                : $"Pareti disponibili nella sala {selectedRoom!.Name}: {wallCount}",
            WallInfoText = selectedWall is null
                ? "Nessuna parete selezionata."
                : $"Parete {selectedWall.Name} nella sala {selectedWall.RoomName} - Pannelli: {selectedWall.Panels.Count}",
            PanelEditorModeText = panelEditorState.ModeText,
            SelectedPanelSummaryText = selectedPanel is null
                ? "Seleziona un pannello dall'elenco per modificarlo, oppure aggiungine uno nuovo."
                : selectedPanel.Summary,
            ShowEmptyPanels = selectedWall is null || selectedWall.Panels.Count == 0,
            WallImageInfoText = selectedPanel is null
                ? "Seleziona un pannello per gestire la sua immagine."
                : panelHasImage
                    ? $"Immagine del pannello {selectedPanel.Name}: {Path.GetFileName(selectedPanel.ImagePath)}"
                    : $"Nessuna immagine associata al pannello {selectedPanel.Name}.",
            WallImageOffsetXText = ToEditorText(selectedPanel?.ImageOffsetX ?? 0d),
            WallImageOffsetYText = ToEditorText(selectedPanel?.ImageOffsetY ?? 0d),
            WallImageScale = selectedPanel is null || selectedPanel.ImageScale <= 0 ? 1d : selectedPanel.ImageScale,
            WallImageOpacity = selectedPanel is null || selectedPanel.ImageOpacity <= 0 ? 0.55d : selectedPanel.ImageOpacity,
            WallImageCropLeftText = ToPercentEditorText(selectedPanel?.ImageCropLeft ?? 0d),
            WallImageCropTopText = ToPercentEditorText(selectedPanel?.ImageCropTop ?? 0d),
            WallImageCropRightText = ToPercentEditorText(selectedPanel?.ImageCropRight ?? 0d),
            WallImageCropBottomText = ToPercentEditorText(selectedPanel?.ImageCropBottom ?? 0d)
        };
    }

    private static (string Title, string Message, string NextAction) BuildWorkflowState(
        RoomDefinition? selectedRoom,
        WallDefinition? selectedWall,
        int roomCount,
        int wallCount,
        int panelCount)
    {
        if (roomCount == 0)
        {
            return ("Passo 1: crea una sala", "Definisci la prima sala della palestra. Dopo potrai aggiungere le pareti.", "Aggiungi una sala.");
        }

        if (selectedRoom is null)
        {
            return ("Seleziona una sala", "Scegli una sala esistente per vedere o aggiungere le sue pareti.", "Seleziona la sala su cui lavorare.");
        }

        if (wallCount == 0)
        {
            return ("Passo 2: aggiungi una parete", $"La sala {selectedRoom.Name} e' pronta. Ora crea la prima parete.", "Compila i dati della parete e premi Aggiungi parete.");
        }

        if (selectedWall is null)
        {
            return ("Seleziona una parete", "Scegli una parete esistente per configurare i suoi pannelli, i fori e il mapping.", "Seleziona la parete da configurare.");
        }

        if (panelCount == 0)
        {
            return ("Passo 3: aggiungi i pannelli", $"La parete {selectedWall.Name} e' pronta. Inserisci il primo pannello.", "Compila i dati del pannello e premi Aggiungi pannello.");
        }

        return ("Parete pronta per il salvataggio", $"Hai {panelCount} pannelli su {selectedWall.Name}. Puoi rifinire l'immagine di un pannello oppure salvare la parete.", "Seleziona un pannello per modificarlo oppure salva la parete.");
    }

    private static string ToEditorText(double value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ToPercentEditorText(double value)
    {
        return (value * 100d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
