namespace RuoteLab.Models;

public sealed class PanelEditorState
{
    public string ModeText { get; init; } = "Inserimento nuovo pannello";

    public string PanelNameText { get; init; } = "Pannello A";

    public string PanelXText { get; init; } = "100";

    public string PanelYText { get; init; } = "100";

    public string PanelWidthText { get; init; } = "1200";

    public string PanelHeightText { get; init; } = "2200";

    public string HoleOffsetText { get; init; } = "40";

    public string HoleOffsetYText { get; init; } = "40";

    public string HoleHorizontalText { get; init; } = "200";

    public string HoleVerticalText { get; init; } = "200";

    public LedRoutingAxis LedRoutingAxis { get; init; } = LedRoutingAxis.Vertical;

    public LedStartDirection LedStartDirection { get; init; } = LedStartDirection.BottomToTop;
}
