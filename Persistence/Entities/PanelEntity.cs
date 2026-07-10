using SQLite;

namespace WallPanelPlanner.Persistence.Entities;

[Table("panels")]
public sealed class PanelEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WallId { get; set; }

    public string Name { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public double HorizontalSpacing { get; set; }

    public double VerticalSpacing { get; set; }

    public double EdgeOffsetX { get; set; }

    public double EdgeOffsetY { get; set; }
}
