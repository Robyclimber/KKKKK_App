using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public sealed class HomeStateService : IHomeStateService
{
    private readonly IRoomRepository roomRepository;
    private readonly IWallRepository wallRepository;
    private readonly ICircuitRepository circuitRepository;

    public HomeStateService(IRoomRepository roomRepository, IWallRepository wallRepository, ICircuitRepository circuitRepository)
    {
        this.roomRepository = roomRepository;
        this.wallRepository = wallRepository;
        this.circuitRepository = circuitRepository;
    }

    public async Task<HomeStateSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await roomRepository.GetAllAsync(cancellationToken);
        var walls = await wallRepository.GetAllAsync(cancellationToken);
        var circuits = await circuitRepository.GetAllAsync(cancellationToken);

        return new HomeStateSummary
        {
            WorkflowState = DetermineState(rooms.Count, circuits.Count),
            RoomsCount = rooms.Count,
            WallsCount = walls.Count,
            CircuitsCount = circuits.Count
        };
    }

    private static HomeWorkflowState DetermineState(int roomsCount, int circuitsCount)
    {
        if (roomsCount <= 0)
        {
            return HomeWorkflowState.SetupPalestra;
        }

        return circuitsCount <= 0
            ? HomeWorkflowState.PrimiCircuiti
            : HomeWorkflowState.Operativo;
    }
}
