using RuoteLab.Models;

namespace RuoteLab.Services;

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

        ValidateLedRouting(input.LedRoutingAxis, input.LedStartDirection);

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
            EdgeOffsetY = input.EdgeOffsetY,
            LedRoutingAxis = input.LedRoutingAxis,
            LedStartDirection = input.LedStartDirection,
            ImagePath = currentPanel?.ImagePath,
            ImageOffsetX = currentPanel?.ImageOffsetX ?? 0d,
            ImageOffsetY = currentPanel?.ImageOffsetY ?? 0d,
            ImageScale = currentPanel?.ImageScale ?? 1d,
            ImageOpacity = currentPanel?.ImageOpacity ?? 0.55d,
            ImageCropLeft = currentPanel?.ImageCropLeft ?? 0d,
            ImageCropTop = currentPanel?.ImageCropTop ?? 0d,
            ImageCropRight = currentPanel?.ImageCropRight ?? 0d,
            ImageCropBottom = currentPanel?.ImageCropBottom ?? 0d
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

    private static void ValidateLedRouting(LedRoutingAxis axis, LedStartDirection direction)
    {
        var isValid = axis switch
        {
            LedRoutingAxis.Vertical => direction is LedStartDirection.BottomToTop or LedStartDirection.TopToBottom,
            LedRoutingAxis.Horizontal => direction is LedStartDirection.LeftToRight or LedStartDirection.RightToLeft,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException("La direzione iniziale LED non e' coerente con l'asse scelto.");
        }
    }

    public void SetPanelImage(PanelDefinition panel, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new InvalidOperationException("Percorso immagine non valido.");
        }

        panel.ImagePath = imagePath;
        panel.ImageOffsetX = 0;
        panel.ImageOffsetY = 0;
        panel.ImageScale = 1d;
        panel.ImageOpacity = 0.55d;
        panel.ImageCropLeft = 0d;
        panel.ImageCropTop = 0d;
        panel.ImageCropRight = 0d;
        panel.ImageCropBottom = 0d;
        ResetPanelImagePerspective(panel);
    }

    public void ClearPanelImage(PanelDefinition panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        panel.ImagePath = null;
        panel.ImageOffsetX = 0;
        panel.ImageOffsetY = 0;
        panel.ImageScale = 1d;
        panel.ImageOpacity = 0.55d;
        panel.ImageCropLeft = 0d;
        panel.ImageCropTop = 0d;
        panel.ImageCropRight = 0d;
        panel.ImageCropBottom = 0d;
        ResetPanelImagePerspective(panel);
    }

    public void UpdatePanelImageAlignment(PanelDefinition panel, double offsetX, double offsetY, double scale, double opacity)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (scale <= 0)
        {
            throw new InvalidOperationException("La scala immagine deve essere positiva.");
        }

        if (opacity <= 0 || opacity > 1)
        {
            throw new InvalidOperationException("L'opacita immagine deve essere compresa tra 0 e 1.");
        }

        panel.ImageOffsetX = offsetX;
        panel.ImageOffsetY = offsetY;
        panel.ImageScale = scale;
        panel.ImageOpacity = opacity;
    }

    public void UpdatePanelImageCrop(PanelDefinition panel, double cropLeft, double cropTop, double cropRight, double cropBottom)
    {
        ArgumentNullException.ThrowIfNull(panel);

        cropLeft = Math.Clamp(cropLeft, 0d, 0.999d);
        cropTop = Math.Clamp(cropTop, 0d, 0.999d);
        cropRight = Math.Clamp(cropRight, 0d, 0.999d);
        cropBottom = Math.Clamp(cropBottom, 0d, 0.999d);

        if (cropLeft + cropRight >= 0.999d)
        {
            throw new InvalidOperationException("Il ritaglio orizzontale e' troppo grande.");
        }

        if (cropTop + cropBottom >= 0.999d)
        {
            throw new InvalidOperationException("Il ritaglio verticale e' troppo grande.");
        }

        panel.ImageCropLeft = cropLeft;
        panel.ImageCropTop = cropTop;
        panel.ImageCropRight = cropRight;
        panel.ImageCropBottom = cropBottom;
    }

    public void UpdatePanelImagePerspective(
        PanelDefinition panel,
        double topLeftX,
        double topLeftY,
        double topRightX,
        double topRightY,
        double bottomLeftX,
        double bottomLeftY,
        double bottomRightX,
        double bottomRightY)
    {
        ArgumentNullException.ThrowIfNull(panel);

        panel.ImagePerspectiveTopLeftX = Math.Clamp(topLeftX, 0d, 1d);
        panel.ImagePerspectiveTopLeftY = Math.Clamp(topLeftY, 0d, 1d);
        panel.ImagePerspectiveTopRightX = Math.Clamp(topRightX, 0d, 1d);
        panel.ImagePerspectiveTopRightY = Math.Clamp(topRightY, 0d, 1d);
        panel.ImagePerspectiveBottomLeftX = Math.Clamp(bottomLeftX, 0d, 1d);
        panel.ImagePerspectiveBottomLeftY = Math.Clamp(bottomLeftY, 0d, 1d);
        panel.ImagePerspectiveBottomRightX = Math.Clamp(bottomRightX, 0d, 1d);
        panel.ImagePerspectiveBottomRightY = Math.Clamp(bottomRightY, 0d, 1d);
    }

    private static void ResetPanelImagePerspective(PanelDefinition panel)
    {
        panel.ImagePerspectiveTopLeftX = 0d;
        panel.ImagePerspectiveTopLeftY = 0d;
        panel.ImagePerspectiveTopRightX = 1d;
        panel.ImagePerspectiveTopRightY = 0d;
        panel.ImagePerspectiveBottomLeftX = 0d;
        panel.ImagePerspectiveBottomLeftY = 1d;
        panel.ImagePerspectiveBottomRightX = 1d;
        panel.ImagePerspectiveBottomRightY = 1d;
    }
}
