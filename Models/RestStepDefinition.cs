namespace RouteLab.Models;

public sealed class RestStepDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = "Recupero";

    public string RoomName { get; init; } = string.Empty;

    public string WallId { get; init; } = string.Empty;

    public string WallName { get; init; } = string.Empty;

    public int DurationSeconds { get; init; } = 60;

    public string BlinkColor { get; init; } = "#FF0000";

    public string CompletedColor { get; init; } = "#00FF00";

    public int BlinkPeriodMs { get; init; } = 500;

    public int CompletedHoldSeconds { get; init; } = 3;
}

