using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IGymSetupService
{
    WallDefinition CreateWall(string roomName, WallInput input, string fallbackWallName);

    WallDefinition UpdateWall(WallDefinition currentWall, string roomName, WallInput input);

    PanelDefinition CreatePanel(PanelInput input, WallDefinition wall, PanelDefinition? currentPanel);

    void SetPanelImage(PanelDefinition panel, string imagePath);

    void ClearPanelImage(PanelDefinition panel);

    void UpdatePanelImageAlignment(PanelDefinition panel, double offsetX, double offsetY, double scale, double opacity);

    void UpdatePanelImageCrop(PanelDefinition panel, double cropLeft, double cropTop, double cropRight, double cropBottom);
}
