using SQLite;

namespace WallPanelPlanner.Persistence.Entities;

[Table("walls")]
public sealed class WallEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string RoomName { get; set; } = "Sala Arrampicata";

    [Indexed]
    public string Name { get; set; } = string.Empty;

    public double Width { get; set; }

    public double Height { get; set; }

    public string? ImagePath { get; set; }

    public double ImageOffsetX { get; set; }

    public double ImageOffsetY { get; set; }

    public double ImageScale { get; set; }

    public double ImageOpacity { get; set; }

    public long UpdatedAtUtcTicks { get; set; }
}
