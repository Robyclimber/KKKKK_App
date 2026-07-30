namespace RouteLab.Models;

public sealed class ResistanceStepDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = "Resistenza";

    public string RoomName { get; init; } = string.Empty;

    public string WallId { get; init; } = string.Empty;

    public string WallName { get; init; } = string.Empty;

    public int DurationSeconds { get; init; } = 120;

    public string ActiveColor { get; init; } = "#FF8C00";

    public string CompletedColor { get; init; } = "#00FF00";

    public string ActiveMode { get; init; } = "steady";

    public int BlinkPeriodMs { get; init; } = 1000;

    public int CompletedHoldSeconds { get; init; } = 3;
}

