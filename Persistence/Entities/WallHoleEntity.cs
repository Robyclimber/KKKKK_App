using SQLite;

namespace RuoteLab.Persistence.Entities;

[Table("wall_holes")]
public sealed class WallHoleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WallId { get; set; }

    public string PanelName { get; set; } = string.Empty;

    public double PanelX { get; set; }

    public double PanelY { get; set; }

    public double RelativeX { get; set; }

    public double RelativeY { get; set; }

    public double AbsoluteX { get; set; }

    public double AbsoluteY { get; set; }

    public string PointId { get; set; } = string.Empty;

    public int LedIndex { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool HasHold { get; set; }

    public int HoldSize { get; set; }

    public int HoldType { get; set; }

    public bool HasEstimatedHoldMetadata { get; set; } = true;
}
