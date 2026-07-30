using System.Collections.ObjectModel;
using WallPanelPlanner.Models;
using WallPanelPlanner.Services;

namespace WallPanelPlanner.ViewModels;

public sealed class CircuitEditorViewModel
{
    private readonly ICircuitEditingService circuitEditingService;
    private readonly GymSetupViewModel roomViewModel;
    private readonly ICircuitRepository circuitRepository;

    public CircuitEditorViewModel(ICircuitEditingService circuitEditingService, GymSetupViewModel roomViewModel, ICircuitRepository circuitRepository)
    {
        this.circuitEditingService = circuitEditingService;
        this.roomViewModel = roomViewModel;
        this.circuitRepository = circuitRepository;
    }

    public ObservableCollection<CircuitDefinition> Circuits { get; } = new();

    public ObservableCollection<WallDefinition> AvailableWalls => roomViewModel.Walls;

    public string? SelectedRoomName { get; private set; }

    public CircuitDefinition? SelectedCircuit { get; private set; }

    public WallDefinition? CurrentWall =>
        SelectedCircuit is not null
            ? roomViewModel.Walls.FirstOrDefault(wall =>
                string.Equals(wall.RoomName, SelectedCircuit.RoomName, StringComparison.Ordinal) &&
                string.Equals(wall.Name, SelectedCircuit.WallName, StringComparison.Ordinal))
            : GetWallsForSelectedRoom().FirstOrDefault();

    public string SuggestedCircuitName => $"Circuito {Circuits.Count + 1}";

    public string CurrentWallLabel =>
        CurrentWall is null
            ? "Nessuna parete disponibile."
            : $"Sala: {CurrentWall.RoomName} - Parete del circuito: {CurrentWall.Name}";

    public IReadOnlyList<string> GetAvailableRooms() =>
        roomViewModel.AvailableRoomNames;

    public IReadOnlyList<WallDefinition> GetWallsForSelectedRoom()
    {
        var targetRoom = SelectedRoomName;
        return AvailableWalls
            .Where(wall => string.IsNullOrWhiteSpace(targetRoom) || string.Equals(wall.RoomName, targetRoom, StringComparison.Ordinal))
            .OrderBy(wall => wall.Name)
            .ToList();
    }

    public IReadOnlyList<CircuitDefinition> GetVisibleCircuits()
    {
        var targetRoom = SelectedRoomName;
        return Circuits
            .Where(circuit =>
            {
                var wall = AvailableWalls.FirstOrDefault(item =>
                    string.Equals(item.RoomName, circuit.RoomName, StringComparison.Ordinal) &&
                    string.Equals(item.Name, circuit.WallName, StringComparison.Ordinal));
                return wall is not null
                       && string.Equals(circuit.RoomName, wall.RoomName, StringComparison.Ordinal)
                       && (string.IsNullOrWhiteSpace(targetRoom) || string.Equals(circuit.RoomName, targetRoom, StringComparison.Ordinal));
            })
            .OrderBy(circuit => circuit.Name)
            .ToList();
    }

    public void SetSelectedRoom(string? roomName)
    {
        SelectedRoomName = string.IsNullOrWhiteSpace(roomName) ? GetAvailableRooms().FirstOrDefault() : roomName;

        if (SelectedCircuit is not null)
        {
            var currentCircuitWall = AvailableWalls.FirstOrDefault(wall =>
                string.Equals(wall.RoomName, SelectedCircuit.RoomName, StringComparison.Ordinal) &&
                string.Equals(wall.Name, SelectedCircuit.WallName, StringComparison.Ordinal));
            if (currentCircuitWall is null ||
                !string.Equals(currentCircuitWall.RoomName, SelectedCircuit.RoomName, StringComparison.Ordinal) ||
                !string.Equals(currentCircuitWall.RoomName, SelectedRoomName, StringComparison.Ordinal))
            {
                SelectedCircuit = GetVisibleCircuits().FirstOrDefault();
            }
        }
        else
        {
            SelectedCircuit = GetVisibleCircuits().FirstOrDefault();
        }
    }

    public void EnsureSelectedRoom()
    {
        if (string.IsNullOrWhiteSpace(SelectedRoomName))
        {
            SelectedRoomName = GetAvailableRooms().FirstOrDefault();
        }
    }

    public async Task CreateCircuitAsync(string? name, string? difficulty, string? inclination, WallDefinition? wall, CancellationToken cancellationToken = default)
    {
        if (wall is null)
        {
            throw new InvalidOperationException("Crea prima almeno una parete nella Sala Arrampicata.");
        }

        var circuit = circuitEditingService.CreateCircuit(name, difficulty, inclination, wall, SuggestedCircuitName);

        Circuits.Add(circuit);
        SelectedCircuit = circuit;
        await circuitRepository.SaveAsync(circuit, cancellationToken);
    }

    public void SelectCircuit(CircuitDefinition? circuit)
    {
        SelectedCircuit = circuit;
        if (circuit is null)
        {
            return;
        }

        var circuitWall = AvailableWalls.FirstOrDefault(wall =>
            string.Equals(wall.RoomName, circuit.RoomName, StringComparison.Ordinal) &&
            string.Equals(wall.Name, circuit.WallName, StringComparison.Ordinal));
        if (circuitWall is not null)
        {
            SelectedRoomName = circuitWall.RoomName;
        }
    }

    public async Task UpdateSelectedCircuitAsync(string? name, string? difficulty, string? inclination, CancellationToken cancellationToken = default)
    {
        if (SelectedCircuit is null)
        {
            throw new InvalidOperationException("Seleziona un circuito da aggiornare.");
        }

        circuitEditingService.UpdateCircuitMetadata(SelectedCircuit, name, difficulty, inclination);
        await circuitRepository.SaveAsync(SelectedCircuit, cancellationToken);
    }

    public async Task DeleteSelectedCircuitAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCircuit is null)
        {
            throw new InvalidOperationException("Seleziona un circuito da eliminare.");
        }

        await circuitRepository.DeleteAsync(SelectedCircuit.Id, cancellationToken);
        Circuits.Remove(SelectedCircuit);
        SelectedCircuit = null;
    }

    public void StartNewCircuitDraft()
    {
        SelectedCircuit = null;
    }

    public async Task ToggleMovementAsync(WallHoleDefinition hole, HandSide hand, MovementRole role, CancellationToken cancellationToken = default)
    {
        if (SelectedCircuit is null)
        {
            throw new InvalidOperationException("Crea o seleziona prima un circuito.");
        }

        if (CurrentWall is null)
        {
            throw new InvalidOperationException("La parete associata al circuito non e' disponibile.");
        }

        circuitEditingService.ToggleMovement(SelectedCircuit, CurrentWall.Name, hole, hand, role);
        await circuitRepository.SaveAsync(SelectedCircuit, cancellationToken);
    }

    public async Task RemoveHoleAsync(WallHoleDefinition hole, CancellationToken cancellationToken = default)
    {
        if (SelectedCircuit is null)
        {
            throw new InvalidOperationException("Crea o seleziona prima un circuito.");
        }

        if (CurrentWall is null)
        {
            throw new InvalidOperationException("La parete associata al circuito non e' disponibile.");
        }

        circuitEditingService.RemoveHole(SelectedCircuit, CurrentWall.Name, hole);
        await circuitRepository.SaveAsync(SelectedCircuit, cancellationToken);
    }

    public IReadOnlyList<CircuitMovementDefinition> GetOrderedMovements()
    {
        if (SelectedCircuit is null)
        {
            return Array.Empty<CircuitMovementDefinition>();
        }

        return SelectedCircuit.Movements
            .OrderBy(movement => movement.Sequence)
            .ThenBy(movement => movement.Hand)
            .ThenBy(movement => movement.Role)
            .ToList();
    }

    public async Task LoadCircuitsAsync(CancellationToken cancellationToken = default)
    {
        await roomViewModel.LoadWallsAsync(cancellationToken);

        Circuits.Clear();
        var savedCircuits = await circuitRepository.GetAllAsync(cancellationToken);
        foreach (var circuit in savedCircuits)
        {
            Circuits.Add(circuit);
        }

        SelectedRoomName ??= GetAvailableRooms().FirstOrDefault();
        SelectedCircuit = GetVisibleCircuits().FirstOrDefault();
    }

    public string? GetRoomNameForCircuit(CircuitDefinition? circuit)
    {
        if (circuit is null)
        {
            return null;
        }

        return circuit.RoomName;
    }
}
