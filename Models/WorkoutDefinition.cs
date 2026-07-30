namespace RuoteLab.Models;

public sealed class WorkoutDefinition
{
    public string WorkoutId { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string RoomName { get; init; } = string.Empty;

    public string WallId { get; init; } = string.Empty;

    public string WallName { get; init; } = string.Empty;

    public List<WorkoutStepDefinition> Steps { get; init; } = new();

    public string DisplayLabel => $"{Name} - {WallName}";
}
