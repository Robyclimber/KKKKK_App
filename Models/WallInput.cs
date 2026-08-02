namespace RouteLab.Models;

public sealed class WallInput
{
    public string RoomName { get; init; } = "Sala Arrampicata";

    public required string Name { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }
}

