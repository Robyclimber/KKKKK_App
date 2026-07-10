using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public sealed class GymSetupService : IGymSetupService
{
    public WallDefinition CreateWall(string roomName, WallInput input, string fallbackWallName)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(roomName))
        {
            throw new InvalidOperationException("Crea o seleziona prima una sala.");
        }

        if (input.Width <= 0 || input.Height <= 0)
        {
            throw new InvalidOperationException("Inserisci larghezza e altezza valide per la parete.");
        }

        return new WallDefinition
        {
            RoomName = roomName,
            Name = string.IsNullOrWhiteSpace(input.Name) ? fallbackWallName : input.Name.Trim(),
            Width = input.Width,
            Height = input.Height
        };
    }

    public WallDefinition UpdateWall(WallDefinition currentWall, string roomName, WallInput input)
    {
        ArgumentNullException.ThrowIfNull(currentWall);

        var updatedWall = CreateWall(roomName, input, currentWall.Name);
        updatedWall.Id = currentWall.Id;
        updatedWall.ImagePath = currentWall.ImagePath;
        updatedWall.ImageOffsetX = currentWall.ImageOffsetX;
        updatedWall.ImageOffsetY = currentWall.ImageOffsetY;
        updatedWall.ImageScale = currentWall.ImageScale;
        updatedWall.ImageOpacity = currentWall.ImageOpacity;

        foreach (var panel in currentWall.Panels)
        {
            if (!updatedWall.Contains(panel))
            {
                throw new InvalidOperationException("Ridimensionando la parete, uno o piu pannelli uscirebbero dai limiti.");
            }

            updatedWall.Panels.Add(panel);
        }

        updatedWall.RegenerateHoleLayoutFromPanels();
        return updatedWall;
    }

    public PanelDefinition CreatePanel(PanelInput input, WallDefinition wall, PanelDefinition? currentPanel)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(wall);

        if (input.Width <= 0 || input.Height <= 0 || input.HorizontalSpacing <= 0 || input.VerticalSpacing <= 0)
        {
            throw new InvalidOperationException("Controlla i valori del pannello e dei fori.");
        }

        if (input.X < 0 || input.Y < 0 || input.EdgeOffsetX < 0 || input.EdgeOffsetY < 0)
        {
            throw new InvalidOperationException("Controlla i valori del pannello e dei fori.");
        }

        var fallbackNumber = currentPanel is null ? wall.Panels.Count + 1 : wall.Panels.IndexOf(currentPanel) + 1;
        var panel = new PanelDefinition
        {
            Name = string.IsNullOrWhiteSpace(input.Name) ? $"Pannello {Math.Max(fallbackNumber, 1)}" : input.Name.Trim(),
            X = input.X,
            Y = input.Y,
            Width = input.Width,
            Height = input.Height,
            HorizontalSpacing = input.HorizontalSpacing,
            VerticalSpacing = input.VerticalSpacing,
            EdgeOffsetX = input.EdgeOffsetX,
            EdgeOffsetY = input.EdgeOffsetY
        };

        if (!wall.Contains(panel))
        {
            throw new InvalidOperationException("Il pannello esce dai limiti della parete selezionata.");
        }

        if (input.EdgeOffsetX > input.Width || input.EdgeOffsetY > input.Height)
        {
            throw new InvalidOperationException("Gli offset iniziali dei fori devono rimanere dentro il pannello.");
        }

        return panel;
    }

    public void SetWallImage(WallDefinition wall, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new InvalidOperationException("Percorso immagine non valido.");
        }

        wall.ImagePath = imagePath;
        wall.ImageOffsetX = 0;
        wall.ImageOffsetY = 0;
        wall.ImageScale = 1d;
        wall.ImageOpacity = 0.55d;
    }

    public void ClearWallImage(WallDefinition wall)
    {
        ArgumentNullException.ThrowIfNull(wall);

        wall.ImagePath = null;
        wall.ImageOffsetX = 0;
        wall.ImageOffsetY = 0;
        wall.ImageScale = 1d;
        wall.ImageOpacity = 0.55d;
    }

    public void UpdateWallImageAlignment(WallDefinition wall, double offsetX, double offsetY, double scale, double opacity)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (scale <= 0)
        {
            throw new InvalidOperationException("La scala immagine deve essere positiva.");
        }

        if (opacity <= 0 || opacity > 1)
        {
            throw new InvalidOperationException("L'opacita immagine deve essere compresa tra 0 e 1.");
        }

        wall.ImageOffsetX = offsetX;
        wall.ImageOffsetY = offsetY;
        wall.ImageScale = scale;
        wall.ImageOpacity = opacity;
    }
}
