namespace RouteLab.Models;

public sealed class HangStepDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = "Sospensione";

    public string RoomName { get; init; } = string.Empty;

    public string WallId { get; init; } = string.Empty;

    public string WallName { get; init; } = string.Empty;

    public int DurationSeconds { get; init; } = 10;

    public List<int> TargetHoleNumbers { get; init; } = new();

    public string ActiveColor { get; init; } = "#00BFFF";

    public string CompletedColor { get; init; } = "#00FF00";

    public int CompletedHoldSeconds { get; init; } = 3;
}

