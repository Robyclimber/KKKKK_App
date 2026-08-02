namespace RouteLab.Models;

public sealed class RoomDefinition
{
    public int Id { get; set; }

    public required string Name { get; init; }

    public string DisplayLabel => Name;
}

