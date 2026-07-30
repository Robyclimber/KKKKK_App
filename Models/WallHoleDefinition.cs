namespace WallPanelPlanner.Models;

public readonly record struct WallHoleDefinition(
    int Number,
    string PanelName,
    double PanelX,
    double PanelY,
    double RelativeX,
    double RelativeY,
    double AbsoluteX,
    double AbsoluteY,
    string PointId = "",
    int LedIndex = 0,
    bool IsEnabled = true,
    bool HasHold = false,
    HoldSize HoldSize = HoldSize.M,
    HoldType HoldType = HoldType.Jug)
{
    public string HoldSummary =>
        !HasHold
            ? "Foro vuoto"
            : $"{GetHoldSizeLabel(HoldSize)} - {GetHoldTypeLabel(HoldType)}";

    public static string GetHoldSizeLabel(HoldSize size) =>
        size switch
        {
            HoldSize.Xs => "XS",
            HoldSize.S => "S",
            HoldSize.M => "M",
            HoldSize.L => "L",
            HoldSize.Xl => "XL",
            HoldSize.Xxl => "XXL",
            _ => "M"
        };

    public static string GetHoldTypeLabel(HoldType type) =>
        type switch
        {
            HoldType.Jug => "Maniglia",
            HoldType.Edge => "Tacca",
            HoldType.Sloper => "Svasa",
            HoldType.Pocket => "Bidito",
            HoldType.Pinch => "Pinza",
            HoldType.Foothold => "Piedi",
            HoldType.Volume => "Volume",
            _ => "Maniglia"
        };
}
