using RouteLab.Models;
using RouteLab.ViewModels;

namespace RouteLab.Services;

public sealed class CircuitPageStateService : ICircuitPageStateService
{
    public CircuitPageState Build(CircuitEditorViewModel viewModel, CircuitInteractionMode interactionMode, HandSide specialModeHand, WallDefinition? currentlySelectedWall)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var rooms = viewModel.GetAvailableRooms().ToList();
        var selectedRoom = viewModel.SelectedRoomName ?? rooms.FirstOrDefault();
        var walls = viewModel.GetWallsForSelectedRoom().ToList();
        var selectedCircuit = viewModel.SelectedCircuit;
        var isEditingExistingCircuit = selectedCircuit is not null;
        var selectedWall = !isEditingExistingCircuit
            ? currentlySelectedWall ?? walls.FirstOrDefault()
            : currentlySelectedWall is not null && selectedCircuit!.UsesWall(currentlySelectedWall.Name)
                ? currentlySelectedWall
                : viewModel.GetWallsForCircuit(selectedCircuit).FirstOrDefault();
        var workflow = BuildWorkflowState(viewModel, rooms.Count, walls.Count);

        return new CircuitPageState
        {
            WorkflowTitleText = workflow.Title,
            WorkflowMessageText = workflow.Message,
            CurrentWallLabel = selectedWall is null
                ? "Nessuna parete disponibile."
                : $"Sala: {selectedWall.RoomName} - Parete attiva: {selectedWall.Name}",
            AvailableRooms = rooms,
            SelectedRoomName = selectedRoom,
            VisibleWalls = walls,
            SelectedWall = selectedWall,
            CanCreateCircuit = selectedWall is not null,
            CanUpdateCircuit = selectedCircuit is not null,
            CanDeleteCircuit = selectedCircuit is not null,
            CanPickWall = selectedWall is not null,
            EditorModeText = selectedCircuit is null
                ? "Nuovo circuito"
                : $"Modifica circuito: {selectedCircuit.Name}",
            CircuitSummaryText = selectedCircuit is null
                ? $"Circuiti nella sala: {viewModel.GetVisibleCircuits().Count}"
                : $"Pareti: {selectedCircuit.WallSummary} - Movimenti: {selectedCircuit.DynamicMovementCount} - Piedi: {selectedCircuit.FootHoldCount}",
            VisibleCircuits = viewModel.GetVisibleCircuits(),
            OrderedMovements = viewModel.GetOrderedMovements(),
            InteractionHintText = interactionMode switch
            {
                CircuitInteractionMode.Select => "Tap sul foro per selezionarlo senza modificare il circuito. Usa poi i pulsanti rapidi o il pannello suggerimento.",
                CircuitInteractionMode.Start => $"Tap sul foro per impostare START con mano {(specialModeHand == HandSide.Right ? "DX" : "SX")}. Dopo il primo start, l'altro passa automaticamente sull'altra mano.",
                CircuitInteractionMode.Top => $"Tap sul foro per impostare TOP con mano {(specialModeHand == HandSide.Right ? "DX" : "SX")}.",
                CircuitInteractionMode.Remove => "Tap sul foro per rimuovere la presa dal circuito.",
                CircuitInteractionMode.Feet => "Modalita Piedi attiva: tocca tutti i fori da accendere insieme durante il circuito.",
                CircuitInteractionMode.LeftHand => "Tap sul foro per inserire il prossimo movimento Mano SX. Dopo l'inserimento il turno passa automaticamente a DX.",
                _ => "Tap sul foro per inserire il prossimo movimento Mano DX. Dopo l'inserimento il turno passa automaticamente a SX."
            }
        };
    }

    private static (string Title, string Message) BuildWorkflowState(CircuitEditorViewModel viewModel, int roomCount, int wallCount)
    {
        if (roomCount == 0)
        {
            return ("Configura prima la palestra", "Prima crea almeno una sala e una parete nella sezione Configura palestra.");
        }

        if (string.IsNullOrWhiteSpace(viewModel.SelectedRoomName))
        {
            return ("Seleziona una sala", "Scegli una sala per vedere i circuiti disponibili e creare il prossimo.");
        }

        if (wallCount == 0)
        {
            return ("Sala senza pareti", $"La sala {viewModel.SelectedRoomName} non ha ancora pareti configurate.");
        }

        if (viewModel.SelectedCircuit is null)
        {
            return ("Nuovo circuito", "Scegli una o piu pareti, inserisci i dati del circuito e crealo.");
        }

        return ("Modifica circuito", "Scegli la parete attiva per disegnare i movimenti; la sequenza resta unica tra tutte le pareti.");
    }
}

