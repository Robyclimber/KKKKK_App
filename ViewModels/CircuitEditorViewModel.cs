using System.Collections.ObjectModel;
using RouteLab.Models;
using RouteLab.Services;

namespace RouteLab.ViewModels;

public sealed class CircuitEditorViewModel
{
    private readonly ICircuitEditingService circuitEditingService;
    private readonly GymSetupViewModel roomViewModel;
    private readonly ICircuitRepository circuitRepository;
    private string? activeWallName;

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
                string.Equals(
                    wall.Name,
                    SelectedCircuit.UsesWall(activeWallName)
                        ? activeWallName
                        : SelectedCircuit.GetWallNames().FirstOrDefault(),
                    StringComparison.Ordinal))
            : GetWallsForSelectedRoom().FirstOrDefault(wall =>
                  string.Equals(wall.Name, activeWallName, StringComparison.Ordinal))
              ?? GetWallsForSelectedRoom().FirstOrDefault();

    public string SuggestedCircuitName => $"Circuito {Circuits.Count + 1}";

    public string CurrentWallLabel =>
        CurrentWall is null
            ? "Nessuna parete disponibile."
            : $"Sala: {CurrentWall.RoomName} - Parete attiva: {CurrentWall.Name}";

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
                var hasAvailableWall = circuit.GetWallNames().Any(wallName =>
                    AvailableWalls.Any(item =>
                        string.Equals(item.RoomName, circuit.RoomName, StringComparison.Ordinal) &&
                        string.Equals(item.Name, wallName, StringComparison.Ordinal)));
                return hasAvailableWall &&
                       (string.IsNullOrWhiteSpace(targetRoom) ||
                        string.Equals(circuit.RoomName, targetRoom, StringComparison.Ordinal));
            })
            .OrderBy(circuit => circuit.Name)
            .ToList();
    }

    public void SetSelectedRoom(string? roomName)
    {
        SelectedRoomName = string.IsNullOrWhiteSpace(roomName) ? GetAvailableRooms().FirstOrDefault() : roomName;

        if (SelectedCircuit is not null)
        {
            if (!string.Equals(SelectedCircuit.RoomName, SelectedRoomName, StringComparison.Ordinal) ||
                GetWallsForCircuit(SelectedCircuit).Count == 0)
            {
                SelectedCircuit = GetVisibleCircuits().FirstOrDefault();
                activeWallName = SelectedCircuit?.GetWallNames().FirstOrDefault();
            }
        }
        else
        {
            activeWallName = GetWallsForSelectedRoom().FirstOrDefault()?.Name;
        }
    }

    public void EnsureSelectedRoom()
    {
        if (string.IsNullOrWhiteSpace(SelectedRoomName))
        {
            SelectedRoomName = GetAvailableRooms().FirstOrDefault();
        }
    }

    public async Task CreateCircuitAsync(string? name, string? difficulty, string? inclination, string? climberProfileId, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals, IReadOnlyList<WallDefinition> walls, CancellationToken cancellationToken = default)
    {
        if (walls.Count == 0)
        {
            throw new InvalidOperationException("Crea prima almeno una parete nella Sala Arrampicata.");
        }

        var circuit = circuitEditingService.CreateCircuit(name, difficulty, inclination, climberProfileId, suggestNextHoldEnabled, globals, walls, SuggestedCircuitName);

        Circuits.Add(circuit);
        SelectedCircuit = circuit;
        activeWallName = circuit.GetWallNames().FirstOrDefault();
        await circuitRepository.SaveAsync(circuit, cancellationToken);
    }

    public void SelectCircuit(CircuitDefinition? circuit)
    {
        SelectedCircuit = circuit;
        if (circuit is null)
        {
            return;
        }

        var circuitWall = GetWallsForCircuit(circuit).FirstOrDefault();
        if (circuitWall is not null)
        {
            SelectedRoomName = circuitWall.RoomName;
            activeWallName = circuitWall.Name;
        }
    }

    public async Task UpdateSelectedCircuitAsync(string? name, string? difficulty, string? inclination, string? climberProfileId, bool suggestNextHoldEnabled, CircuitGlobalsDefinition? globals, IReadOnlyList<WallDefinition> walls, CancellationToken cancellationToken = default)
    {
        if (SelectedCircuit is null)
        {
            throw new InvalidOperationException("Seleziona un circuito da aggiornare.");
        }

        circuitEditingService.UpdateCircuitMetadata(SelectedCircuit, name, difficulty, inclination, climberProfileId, suggestNextHoldEnabled, globals);
        circuitEditingService.UpdateCircuitWalls(SelectedCircuit, walls);
        if (!SelectedCircuit.UsesWall(activeWallName))
        {
            activeWallName = SelectedCircuit.GetWallNames().FirstOrDefault();
        }
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
        activeWallName = null;
    }

    public void StartNewCircuitDraft()
    {
        SelectedCircuit = null;
        activeWallName = GetWallsForSelectedRoom().FirstOrDefault()?.Name;
    }

    public IReadOnlyList<WallDefinition> GetWallsForCircuit(CircuitDefinition? circuit)
    {
        if (circuit is null)
        {
            return Array.Empty<WallDefinition>();
        }

        var wallOrder = circuit.GetWallNames()
            .Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        return AvailableWalls
            .Where(wall =>
                string.Equals(wall.RoomName, circuit.RoomName, StringComparison.Ordinal) &&
                wallOrder.ContainsKey(wall.Name))
            .OrderBy(wall => wallOrder[wall.Name])
            .ToList();
    }

    public void SetActiveWall(WallDefinition? wall)
    {
        if (wall is null)
        {
            return;
        }

        if (SelectedCircuit is not null && !SelectedCircuit.UsesWall(wall.Name))
        {
            throw new InvalidOperationException("La parete selezionata non appartiene al circuito.");
        }

        SelectedRoomName = wall.RoomName;
        activeWallName = wall.Name;
    }

    public void SetSelectedCircuitWallsDraft(IReadOnlyList<WallDefinition> walls)
    {
        if (SelectedCircuit is null)
        {
            return;
        }

        circuitEditingService.UpdateCircuitWalls(SelectedCircuit, walls);
        if (!SelectedCircuit.UsesWall(activeWallName))
        {
            activeWallName = SelectedCircuit.GetWallNames().FirstOrDefault();
        }
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

    public async Task ToggleFootHoldAsync(WallHoleDefinition hole, CancellationToken cancellationToken = default)
    {
        if (SelectedCircuit is null)
        {
            throw new InvalidOperationException("Crea o seleziona prima un circuito.");
        }

        if (CurrentWall is null)
        {
            throw new InvalidOperationException("La parete associata al circuito non e' disponibile.");
        }

        circuitEditingService.ToggleFootHold(SelectedCircuit, CurrentWall.Name, hole);
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
            .OrderBy(movement => movement.IsFootHold ? 0 : 1)
            .ThenBy(movement => movement.Sequence)
            .ThenBy(movement => movement.Hand)
            .ThenBy(movement => movement.Role)
            .ToList();
    }

    public async Task LoadCircuitsAsync(CancellationToken cancellationToken = default)
    {
        await roomViewModel.EnsureLoadedAsync(cancellationToken);

        Circuits.Clear();
        var savedCircuits = await circuitRepository.GetAllAsync(cancellationToken);
        foreach (var circuit in savedCircuits)
        {
            Circuits.Add(circuit);
        }

        SelectedRoomName ??= GetAvailableRooms().FirstOrDefault();
        SelectedCircuit = null;
        activeWallName = GetWallsForSelectedRoom().FirstOrDefault()?.Name;
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
