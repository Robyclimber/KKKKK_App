using SQLite;

namespace WallPanelPlanner.Persistence.Entities;

[Table("circuit_movements")]
public sealed class CircuitMovementEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int CircuitId { get; set; }

    public string WallName { get; set; } = string.Empty;

    public int HoleNumber { get; set; }

    public int Hand { get; set; }

    public int Role { get; set; }

    public int Sequence { get; set; }
}
