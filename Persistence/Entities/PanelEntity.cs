using SQLite;

namespace RuoteLab.Persistence.Entities;

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

    public int LedRoutingAxis { get; set; }

    public int LedStartDirection { get; set; }

    public string? ImagePath { get; set; }

    public double ImageOffsetX { get; set; }

    public double ImageOffsetY { get; set; }

    public double ImageScale { get; set; }

    public double ImageOpacity { get; set; }

    public double ImageCropLeft { get; set; }

    public double ImageCropTop { get; set; }

    public double ImageCropRight { get; set; }

    public double ImageCropBottom { get; set; }

    public double ImagePerspectiveTopLeftX { get; set; }

    public double ImagePerspectiveTopLeftY { get; set; }

    public double ImagePerspectiveTopRightX { get; set; }

    public double ImagePerspectiveTopRightY { get; set; }

    public double ImagePerspectiveBottomLeftX { get; set; }

    public double ImagePerspectiveBottomLeftY { get; set; }

    public double ImagePerspectiveBottomRightX { get; set; }

    public double ImagePerspectiveBottomRightY { get; set; }
}
