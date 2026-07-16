using System.Collections.ObjectModel;

namespace RuoteLab.Models;

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

    public PanelDefinition? FindPanel(string panelName)
    {
        return Panels.FirstOrDefault(panel => string.Equals(panel.Name, panelName, StringComparison.Ordinal));
    }

    public PanelDefinition? FindPanel(WallHoleDefinition hole)
    {
        return FindPanel(hole.PanelName);
    }

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
                var assignedNumber = number++;
                numbered.Add(hole with
                {
                    Number = assignedNumber,
                    PointId = string.IsNullOrWhiteSpace(hole.PointId) ? BuildDefaultPointId(Name, assignedNumber) : hole.PointId,
                    LedIndex = hole.LedIndex > 0 ? hole.LedIndex : assignedNumber,
                    IsEnabled = hole.IsEnabled
                });
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
                    PointId = string.IsNullOrWhiteSpace(existingHole.PointId) ? BuildDefaultPointId(Name, hole.Number) : existingHole.PointId,
                    LedIndex = existingHole.LedIndex > 0 ? existingHole.LedIndex : hole.Number,
                    IsEnabled = existingHole.IsEnabled,
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
            HoldType = holdType,
            HasEstimatedHoldMetadata = false
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
            HasHold = false,
            HasEstimatedHoldMetadata = true
        });
    }

    public void UpdateHoleHardware(int holeNumber, string? pointId, int ledIndex, bool isEnabled)
    {
        var orderedHoles = GetOrderedHoles();
        var targetHole = orderedHoles.FirstOrDefault(hole => hole.Number == holeNumber);
        if (targetHole.Number == 0)
        {
            throw new InvalidOperationException("Foro non trovato.");
        }

        if (ledIndex <= 0)
        {
            throw new InvalidOperationException("L'indice LED deve essere maggiore di zero.");
        }

        var normalizedPointId = string.IsNullOrWhiteSpace(pointId)
            ? BuildDefaultPointId(Name, holeNumber)
            : pointId.Trim();

        ReplaceHoleMetadata(targetHole with
        {
            PointId = normalizedPointId,
            LedIndex = ledIndex,
            IsEnabled = isEnabled
        });

        ValidateHardwareMappings();
    }

    public void AutoAssignLedIndicesByPanelRouting()
    {
        if (HoleLayout.Count == 0)
        {
            RegenerateHoleLayoutFromPanels();
        }

        var orderedHoles = GetOrderedHoles();
        var holesByKey = orderedHoles.ToDictionary(
            hole => BuildHoleMetadataKey(hole.PanelName, hole.RelativeX, hole.RelativeY),
            hole => hole);

        var nextLedIndex = 1;
        foreach (var panel in Panels
                     .OrderBy(panel => panel.Y)
                     .ThenBy(panel => panel.X)
                     .ThenBy(panel => panel.Name, StringComparer.Ordinal))
        {
            foreach (var panelHole in panel.GetOrderedHoles())
            {
                var metadataKey = BuildHoleMetadataKey(panel.Name, panelHole.X, panelHole.Y);
                if (!holesByKey.TryGetValue(metadataKey, out var wallHole))
                {
                    continue;
                }

                ReplaceHoleMetadata(wallHole with
                {
                    PointId = string.IsNullOrWhiteSpace(wallHole.PointId)
                        ? BuildDefaultPointId(Name, wallHole.Number)
                        : wallHole.PointId,
                    LedIndex = nextLedIndex++
                });
            }
        }

        ValidateHardwareMappings();
    }

    public void ValidateHardwareMappings()
    {
        var orderedHoles = GetOrderedHoles();

        var duplicateLedIndices = orderedHoles
            .Where(hole => hole.IsEnabled)
            .GroupBy(hole => hole.LedIndex)
            .Where(group => group.Key > 0 && group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value)
            .ToList();

        if (duplicateLedIndices.Count > 0)
        {
            throw new InvalidOperationException($"Conflitto mapping LED: i LED {string.Join(", ", duplicateLedIndices)} sono assegnati a piu fori.");
        }

        var duplicatePointIds = orderedHoles
            .Where(hole => !string.IsNullOrWhiteSpace(hole.PointId))
            .GroupBy(hole => hole.PointId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value)
            .ToList();

        if (duplicatePointIds.Count > 0)
        {
            throw new InvalidOperationException($"Conflitto pointId: {string.Join(", ", duplicatePointIds)}");
        }
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
                string.Empty,
                0,
                true,
                false,
                HoldSize.M,
                HoldType.Jug,
                true)))
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

    private static string BuildDefaultPointId(string wallName, int holeNumber)
    {
        var safeWallName = string.IsNullOrWhiteSpace(wallName)
            ? "wall"
            : wallName.Trim().ToLowerInvariant().Replace(' ', '-');
        return $"{safeWallName}-hole-{holeNumber:000}";
    }
}
