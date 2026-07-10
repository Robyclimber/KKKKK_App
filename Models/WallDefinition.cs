using System.Collections.ObjectModel;

namespace WallPanelPlanner.Models;

public sealed class WallDefinition
{
    public int Id { get; set; }

    public string RoomName { get; init; } = "Sala Arrampicata";

    public required string Name { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public string? ImagePath { get; set; }

    public double ImageOffsetX { get; set; }

    public double ImageOffsetY { get; set; }

    public double ImageScale { get; set; } = 1d;

    public double ImageOpacity { get; set; } = 0.55d;

    public ObservableCollection<PanelDefinition> Panels { get; } = new();

    public ObservableCollection<WallHoleDefinition> HoleLayout { get; } = new();

    public string DisplayLabel => $"{RoomName} - {Name} - {Width:0.#} x {Height:0.#} mm";

    public IReadOnlyList<WallHoleDefinition> GetOrderedHoles()
    {
        var allHoles = HoleLayout.Count > 0
            ? HoleLayout.ToList()
            : BuildHoleLayoutFromPanels();

        if (allHoles.Count == 0)
        {
            return Array.Empty<WallHoleDefinition>();
        }

        const double tolerance = 0.0001d;
        var orderedColumns = allHoles
            .OrderBy(hole => hole.AbsoluteX)
            .ThenBy(hole => hole.AbsoluteY)
            .GroupBy(hole => Math.Round(hole.AbsoluteX / tolerance) * tolerance)
            .OrderBy(group => group.Key)
            .ToList();

        var numbered = new List<WallHoleDefinition>(allHoles.Count);
        var number = 1;

        for (var columnIndex = 0; columnIndex < orderedColumns.Count; columnIndex++)
        {
            var holesInColumn = columnIndex % 2 == 0
                ? orderedColumns[columnIndex].OrderBy(hole => hole.AbsoluteY)
                : orderedColumns[columnIndex].OrderByDescending(hole => hole.AbsoluteY);

            foreach (var hole in holesInColumn)
            {
                numbered.Add(hole with { Number = number++ });
            }
        }

        return numbered;
    }

    public void RegenerateHoleLayoutFromPanels()
    {
        var existingMetadata = HoleLayout.ToDictionary(
            hole => BuildHoleMetadataKey(hole.PanelName, hole.RelativeX, hole.RelativeY),
            hole => hole);

        HoleLayout.Clear();
        foreach (var hole in BuildHoleLayoutFromPanels())
        {
            if (existingMetadata.TryGetValue(BuildHoleMetadataKey(hole.PanelName, hole.RelativeX, hole.RelativeY), out var existingHole))
            {
                HoleLayout.Add(hole with
                {
                    HasHold = existingHole.HasHold,
                    HoldSize = existingHole.HoldSize,
                    HoldType = existingHole.HoldType
                });
                continue;
            }

            HoleLayout.Add(hole);
        }
    }

    public void SetHoleHold(int holeNumber, HoldSize holdSize, HoldType holdType)
    {
        var orderedHoles = GetOrderedHoles();
        var targetHole = orderedHoles.FirstOrDefault(hole => hole.Number == holeNumber);
        if (targetHole.Number == 0)
        {
            throw new InvalidOperationException("Foro non trovato.");
        }

        ReplaceHoleMetadata(targetHole with
        {
            HasHold = true,
            HoldSize = holdSize,
            HoldType = holdType
        });
    }

    public void ClearHoleHold(int holeNumber)
    {
        var orderedHoles = GetOrderedHoles();
        var targetHole = orderedHoles.FirstOrDefault(hole => hole.Number == holeNumber);
        if (targetHole.Number == 0)
        {
            throw new InvalidOperationException("Foro non trovato.");
        }

        ReplaceHoleMetadata(targetHole with
        {
            HasHold = false
        });
    }

    public bool Contains(PanelDefinition panel)
    {
        return panel.X >= 0
            && panel.Y >= 0
            && panel.X + panel.Width <= Width
            && panel.Y + panel.Height <= Height;
    }

    private List<WallHoleDefinition> BuildHoleLayoutFromPanels()
    {
        return Panels
            .SelectMany(panel => panel.GetOrderedHoles().Select(hole => new WallHoleDefinition(
                0,
                panel.Name,
                panel.X,
                panel.Y,
                hole.X,
                hole.Y,
                panel.X + hole.X,
                panel.Y + hole.Y,
                false,
                HoldSize.M,
                HoldType.Jug)))
            .ToList();
    }

    private void ReplaceHoleMetadata(WallHoleDefinition replacement)
    {
        for (var index = 0; index < HoleLayout.Count; index++)
        {
            var current = HoleLayout[index];
            if (current.PanelName == replacement.PanelName &&
                Math.Abs(current.RelativeX - replacement.RelativeX) < 0.0001d &&
                Math.Abs(current.RelativeY - replacement.RelativeY) < 0.0001d)
            {
                HoleLayout[index] = replacement with { Number = 0 };
                return;
            }
        }
    }

    private static string BuildHoleMetadataKey(string panelName, double relativeX, double relativeY)
    {
        return $"{panelName}|{relativeX:0.####}|{relativeY:0.####}";
    }
}
