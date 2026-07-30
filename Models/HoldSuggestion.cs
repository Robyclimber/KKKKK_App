namespace WallPanelPlanner.Models;

public sealed class HoldSuggestion
{
    public bool HasHold { get; init; }

    public HoldSize HoldSize { get; init; } = HoldSize.M;

    public HoldType HoldType { get; init; } = HoldType.Jug;

    public string Reason { get; init; } = string.Empty;
}
