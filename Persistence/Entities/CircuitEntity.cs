using SQLite;

namespace RouteLab.Persistence.Entities;

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
    public string CircuitId { get; set; } = string.Empty;

    [Indexed]
    public string WallName { get; set; } = string.Empty;

    public string WallNamesJson { get; set; } = "[]";

    public string Difficulty { get; set; } = string.Empty;

    public string Inclination { get; set; } = string.Empty;

    public string ClimberProfileId { get; set; } = "default";

    public bool SuggestNextHoldEnabled { get; set; }

    public string PresetName { get; set; } = "default";

    public string Effect { get; set; } = "steady";

    public int DefaultBrightness { get; set; } = 96;

    public int DimmedBrightness { get; set; } = 48;

    public string RightHandColor { get; set; } = "#C44536";

    public string LeftHandColor { get; set; } = "#247BA0";

    public string StartColor { get; set; } = "#FFFF00";

    public string TopColor { get; set; } = "#FF0000";

    public int BlinkCount { get; set; } = 3;

    public int BlinkPeriodMs { get; set; } = 250;

    public int HoldDurationMs { get; set; } = 2500;

    public long UpdatedAtUtcTicks { get; set; }
}

