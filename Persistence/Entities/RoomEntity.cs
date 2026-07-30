using SQLite;

namespace RuoteLab.Persistence.Entities;

[Table("rooms")]
public sealed class RoomEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Name { get; set; } = string.Empty;
}
