using SQLite;

namespace WallPanelPlanner.Persistence.Entities;

[Table("circuits")]
public sealed class CircuitEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string RoomName { get; set; } = "Sala Arrampicata";

    [Indexed]
    public string Name { get; set; } = string.Empty;

    [Indexed]
    public string WallName { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    public string Inclination { get; set; } = string.Empty;

    public long UpdatedAtUtcTicks { get; set; }
}
