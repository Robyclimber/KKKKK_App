namespace WallPanelPlanner.Models;

public sealed class HomeStateSummary
{
    public required HomeWorkflowState WorkflowState { get; init; }

    public int RoomsCount { get; init; }

    public int WallsCount { get; init; }

    public int CircuitsCount { get; init; }
}
