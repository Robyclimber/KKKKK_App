using System.Collections.ObjectModel;
using WallPanelPlanner.Models;
using WallPanelPlanner.Services;

namespace WallPanelPlanner.ViewModels;

public class GymSetupViewModel
{
    private readonly IGymSetupService gymSetupService;
    private readonly IWallConfigurationStorageService storageService;
    private readonly IWallRepository wallRepository;
    private readonly IRoomRepository roomRepository;

    public GymSetupViewModel(
        IGymSetupService gymSetupService,
        IWallConfigurationStorageService storageService,
        IWallRepository wallRepository,
        IRoomRepository roomRepository)
    {
        this.gymSetupService = gymSetupService;
        this.storageService = storageService;
        this.wallRepository = wallRepository;
        this.roomRepository = roomRepository;
    }

    public ObservableCollection<RoomDefinition> Rooms { get; } = new();

    public ObservableCollection<WallDefinition> Walls { get; } = new();

    public RoomDefinition? SelectedRoom { get; private set; }

    public WallDefinition? SelectedWall { get; private set; }

    public PanelDefinition? SelectedPanel { get; private set; }

    public string SuggestedNextRoomName => $"Sala {Rooms.Count + 1}";

    public string SuggestedNextWallName => $"Parete {GetWallsForSelectedRoom().Count + 1}";

    public string SuggestedNextPanelName =>
        SelectedWall is null
            ? "Pannello A"
            : $"Pannello {SelectedWall.Panels.Count + 1}";

    public bool HasSelectedWall => SelectedWall is not null;

    public bool HasSelectedPanel => SelectedPanel is not null;

    public IReadOnlyList<string> AvailableRoomNames =>
        Rooms.Select(room => room.Name)
            .OrderBy(name => name)
            .ToList();

    public IReadOnlyList<WallDefinition> GetWallsForSelectedRoom()
    {
        if (SelectedRoom is null)
        {
            return Array.Empty<WallDefinition>();
        }

        return Walls
            .Where(wall => string.Equals(wall.RoomName, SelectedRoom.Name, StringComparison.Ordinal))
            .OrderBy(wall => wall.Name)
            .ToList();
    }

    public async Task AddRoomAsync(string? roomName, CancellationToken cancellationToken = default)
    {
        var normalizedName = roomName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Inserisci un nome sala valido.");
        }

        var existing = Rooms.FirstOrDefault(room => string.Equals(room.Name, normalizedName, StringComparison.Ordinal));
        if (existing is not null)
        {
            SelectRoom(existing);
            return;
        }

        var room = new RoomDefinition
        {
            Name = normalizedName
        };

        await roomRepository.SaveAsync(room, cancellationToken);
        Rooms.Add(room);
        SelectRoom(room);
    }

    public void SelectRoom(RoomDefinition? room)
    {
        SelectedRoom = room;

        if (room is null)
        {
            SelectedWall = null;
            SelectedPanel = null;
            return;
        }

        if (SelectedWall is null || !string.Equals(SelectedWall.RoomName, room.Name, StringComparison.Ordinal))
        {
            SelectedWall = GetWallsForSelectedRoom().FirstOrDefault();
        }

        SelectedPanel = null;
    }

    public void AddWall(WallInput input)
    {
        if (SelectedRoom is null)
        {
            throw new InvalidOperationException("Crea o seleziona prima una sala.");
        }

        var wall = gymSetupService.CreateWall(SelectedRoom.Name, input, SuggestedNextWallName);

        Walls.Add(wall);
        SelectWall(wall);
    }

    public void UpdateSelectedWall(WallInput input)
    {
        if (SelectedRoom is null)
        {
            throw new InvalidOperationException("Crea o seleziona prima una sala.");
        }

        if (SelectedWall is null)
        {
            throw new InvalidOperationException("Seleziona prima una parete da aggiornare.");
        }

        var replacement = gymSetupService.UpdateWall(SelectedWall, SelectedRoom.Name, input);
        var index = Walls.IndexOf(SelectedWall);
        if (index < 0)
        {
            throw new InvalidOperationException("La parete selezionata non e' piu disponibile.");
        }

        Walls[index] = replacement;
        SelectedWall = replacement;
    }

    public void SelectWall(WallDefinition? wall)
    {
        if (wall is not null)
        {
            var room = Rooms.FirstOrDefault(item => string.Equals(item.Name, wall.RoomName, StringComparison.Ordinal));
            if (room is not null)
            {
                SelectedRoom = room;
            }
        }

        SelectedWall = wall;
        SelectedPanel = null;
    }

    public void AddPanel(PanelInput input)
    {
        EnsureWallSelected();
        var panel = gymSetupService.CreatePanel(input, SelectedWall!, null);
        SelectedWall!.Panels.Add(panel);
        SelectedWall.RegenerateHoleLayoutFromPanels();
        SelectedPanel = null;
    }

    public void SelectPanel(PanelDefinition panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (SelectedWall is null || !SelectedWall.Panels.Contains(panel))
        {
            throw new InvalidOperationException("Il pannello selezionato non appartiene alla parete attiva.");
        }

        SelectedPanel = panel;
    }

    public void UpdateSelectedPanel(PanelInput input)
    {
        EnsureWallSelected();
        EnsurePanelSelected();

        var replacement = gymSetupService.CreatePanel(input, SelectedWall!, SelectedPanel);
        var index = SelectedWall!.Panels.IndexOf(SelectedPanel!);
        if (index < 0)
        {
            throw new InvalidOperationException("Il pannello selezionato non e' piu disponibile.");
        }

        SelectedWall.Panels[index] = replacement;
        SelectedWall.RegenerateHoleLayoutFromPanels();
        SelectedPanel = replacement;
    }

    public void DeleteSelectedPanel()
    {
        EnsureWallSelected();
        EnsurePanelSelected();
        SelectedWall!.Panels.Remove(SelectedPanel!);
        SelectedWall.RegenerateHoleLayoutFromPanels();
        SelectedPanel = null;
    }

    public Task<string> SaveSelectedWallAsync(CancellationToken cancellationToken = default)
    {
        EnsureWallSelected();
        return storageService.SaveAsync(SelectedWall!, cancellationToken);
    }

    public async Task LoadWallsAsync(CancellationToken cancellationToken = default)
    {
        Rooms.Clear();
        var savedRooms = await roomRepository.GetAllAsync(cancellationToken);
        foreach (var room in savedRooms)
        {
            Rooms.Add(room);
        }

        Walls.Clear();
        var savedWalls = await wallRepository.GetAllAsync(cancellationToken);
        foreach (var wall in savedWalls)
        {
            Walls.Add(wall);
            if (!Rooms.Any(room => string.Equals(room.Name, wall.RoomName, StringComparison.Ordinal)))
            {
                Rooms.Add(new RoomDefinition
                {
                    Name = wall.RoomName
                });
            }
        }

        SelectedRoom = Rooms.OrderBy(room => room.Name).FirstOrDefault();
        SelectedWall = GetWallsForSelectedRoom().FirstOrDefault();
        SelectedPanel = null;
    }

    public void SetSelectedWallImage(string imagePath)
    {
        EnsureWallSelected();
        gymSetupService.SetWallImage(SelectedWall!, imagePath);
    }

    public void ClearSelectedWallImage()
    {
        EnsureWallSelected();
        gymSetupService.ClearWallImage(SelectedWall!);
    }

    public void UpdateSelectedWallImageAlignment(double offsetX, double offsetY, double scale, double opacity)
    {
        EnsureWallSelected();
        gymSetupService.UpdateWallImageAlignment(SelectedWall!, offsetX, offsetY, scale, opacity);
    }

    public bool IsPanelSelected(PanelDefinition panel)
    {
        return ReferenceEquals(panel, SelectedPanel);
    }

    private void EnsureWallSelected()
    {
        if (SelectedWall is null)
        {
            throw new InvalidOperationException("Seleziona prima una parete.");
        }
    }

    private void EnsurePanelSelected()
    {
        if (SelectedPanel is null)
        {
            throw new InvalidOperationException("Seleziona un pannello da modificare o eliminare.");
        }
    }
}

public sealed class EditRoomViewModel : GymSetupViewModel
{
    public EditRoomViewModel(
        IGymSetupService gymSetupService,
        IWallConfigurationStorageService storageService,
        IWallRepository wallRepository,
        IRoomRepository roomRepository)
        : base(gymSetupService, storageService, wallRepository, roomRepository)
    {
    }
}
