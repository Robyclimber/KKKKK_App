using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public interface IGymSetupService
{
    WallDefinition CreateWall(string roomName, WallInput input, string fallbackWallName);

    WallDefinition UpdateWall(WallDefinition currentWall, string roomName, WallInput input);

    PanelDefinition CreatePanel(PanelInput input, WallDefinition wall, PanelDefinition? currentPanel);

    void SetWallImage(WallDefinition wall, string imagePath);

    void ClearWallImage(WallDefinition wall);

    void UpdateWallImageAlignment(WallDefinition wall, double offsetX, double offsetY, double scale, double opacity);
}
