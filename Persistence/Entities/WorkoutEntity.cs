using SQLite;

namespace RouteLab.Persistence.Entities;

[Table("workouts")]
public sealed class WorkoutEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string WorkoutId { get; set; } = string.Empty;

    [Indexed]
    public string RoomName { get; set; } = string.Empty;

    [Indexed]
    public string WallId { get; set; } = string.Empty;

    [Indexed]
    public string WallName { get; set; } = string.Empty;

    [Indexed]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public long UpdatedAtUtcTicks { get; set; }
}

