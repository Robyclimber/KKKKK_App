using System.Collections.ObjectModel;

namespace RouteLab.Models;

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

    public LedStartDirection LedVerticalDirection { get; set; } = LedStartDirection.TopToBottom;

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
        var allHoles = (HoleLayout.Count > 0
            ? HoleLayout.ToList()
            : BuildHoleLayoutFromPanels())
            .Where(hole => hole.IsEnabled && !hole.IsSuppressed)
            .ToList();

        if (allHoles.Count == 0)
        {
            return Array.Empty<WallHoleDefinition>();
        }

        var numbered = new List<WallHoleDefinition>(allHoles.Count);
        var number = 1;
        foreach (var hole in GetHolesInWallNumberOrder(allHoles))
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

        return numbered;
    }

    public void RegenerateHoleLayoutFromPanels()
    {
        var existingMetadata = HoleLayout
            .GroupBy(hole => BuildHoleMetadataKey(hole.PanelName, hole.RelativeX, hole.RelativeY))
            .ToDictionary(group => group.Key, group => group.First());

        var manualHoles = HoleLayout
            .Where(hole => hole.SourceKind == WallHoleSourceKind.Manual)
            .ToList();

        HoleLayout.Clear();
        foreach (var hole in BuildHoleLayoutFromPanels())
        {
            if (existingMetadata.TryGetValue(BuildHoleMetadataKey(hole.PanelName, hole.RelativeX, hole.RelativeY), out var existingHole))
            {
                if (existingHole.IsSuppressed)
                {
                    HoleLayout.Add(existingHole with { Number = 0 });
                    continue;
                }

                HoleLayout.Add(hole with
                {
                    PointId = existingHole.PointId,
                    LedIndex = existingHole.LedIndex,
                    IsEnabled = existingHole.IsEnabled,
                    HasHold = existingHole.HasHold,
                    HoldSize = existingHole.HoldSize,
                    HoldType = existingHole.HoldType,
                    HasEstimatedHoldMetadata = existingHole.HasEstimatedHoldMetadata,
                    SourceKind = WallHoleSourceKind.Generated,
                    ManualOrder = existingHole.ManualOrder
                });
                continue;
            }

            HoleLayout.Add(hole);
        }

        foreach (var manualHole in manualHoles)
        {
            HoleLayout.Add(manualHole with { Number = 0 });
        }

        AssignMissingGeneratedHardwareMappings();
        AssignLedIndicesByWallRouting();
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
            HasEstimatedHoldMetadata = false
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

        if (isEnabled && ledIndex <= 0)
        {
            throw new InvalidOperationException("L'indice LED deve essere maggiore di zero.");
        }

        var normalizedPointId = string.IsNullOrWhiteSpace(pointId)
            ? BuildDefaultPointId(Name, holeNumber)
            : pointId.Trim();

        ReplaceHoleMetadata(targetHole with
        {
            PointId = normalizedPointId,
            LedIndex = ledIndex > 0 ? ledIndex : targetHole.LedIndex,
            IsEnabled = isEnabled
        });

        ValidateHardwareMappings();
    }

    public int GetNextManualOrder() => HoleLayout.Count(hole => hole.IsEnabled && !hole.IsSuppressed && hole.ManualOrder > 0) + 1;

    public void SetManualHoleOrder(int holeNumber, int manualOrder)
    {
        if (manualOrder <= 0)
        {
            throw new InvalidOperationException("L'ordine manuale deve essere maggiore di zero.");
        }

        var orderedHoles = GetOrderedHoles();
        var targetHole = orderedHoles.FirstOrDefault(hole => hole.Number == holeNumber);
        if (targetHole.Number == 0)
        {
            throw new InvalidOperationException("Foro non trovato.");
        }

        if (HoleLayout.Any(hole => hole.ManualOrder == manualOrder && !IsSameHole(hole, targetHole)))
        {
            throw new InvalidOperationException($"L'ordine {manualOrder} è già assegnato a un altro foro.");
        }

        ReplaceHoleMetadata(targetHole with { ManualOrder = manualOrder });
    }

    public void ClearManualHoleOrder()
    {
        for (var index = 0; index < HoleLayout.Count; index++)
        {
            if (HoleLayout[index].ManualOrder > 0)
            {
                HoleLayout[index] = HoleLayout[index] with { Number = 0, ManualOrder = 0 };
            }
        }
    }

    public void AutoAssignLedIndicesByWallRouting()
    {
        if (HoleLayout.Count == 0)
        {
            RegenerateHoleLayoutFromPanels();
        }

        RemoveDuplicateHoles();
        AssignLedIndicesByWallRouting();
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

    public IReadOnlyList<WallHoleDefinition> GetOrderedHolesForPanel(string panelName)
    {
        return GetOrderedHoles()
            .Where(hole => string.Equals(hole.PanelName, panelName, StringComparison.Ordinal) && !hole.IsSuppressed)
            .ToList();
    }

    public void AddManualHole(string panelName, double relativeX, double relativeY)
    {
        var panel = FindPanel(panelName) ?? throw new InvalidOperationException("Pannello non trovato.");
        if (relativeX < 0d || relativeY < 0d || relativeX > panel.Width || relativeY > panel.Height)
        {
            throw new InvalidOperationException("Il foro manuale deve restare dentro il pannello.");
        }

        var roundedX = RoundCoordinate(relativeX);
        var roundedY = RoundCoordinate(relativeY);
        var key = BuildHoleMetadataKey(panelName, roundedX, roundedY);

        for (var index = 0; index < HoleLayout.Count; index++)
        {
            var current = HoleLayout[index];
            if (!string.Equals(BuildHoleMetadataKey(current.PanelName, current.RelativeX, current.RelativeY), key, StringComparison.Ordinal))
            {
                continue;
            }

            if (current.IsSuppressed)
            {
                HoleLayout[index] = current with
                {
                    Number = 0,
                    IsEnabled = true,
                    SourceKind = WallHoleSourceKind.Manual
                };
                AssignLedIndicesByWallRouting();
                return;
            }

            return;
        }

        HoleLayout.Add(new WallHoleDefinition(
            0,
            panel.Name,
            panel.X,
            panel.Y,
            roundedX,
            roundedY,
            panel.X + roundedX,
            panel.Y + roundedY,
            string.Empty,
            0,
            true,
            false,
            HoldSize.M,
            HoldType.Jug,
            true,
            WallHoleSourceKind.Manual));
        AssignLedIndicesByWallRouting();
    }

    public void RemoveHoleFromPanel(string panelName, int holeNumber)
    {
        var orderedHole = GetOrderedHolesForPanel(panelName).FirstOrDefault(hole => hole.Number == holeNumber);
        if (orderedHole.Number == 0)
        {
            throw new InvalidOperationException("Foro non trovato nel pannello.");
        }

        for (var index = 0; index < HoleLayout.Count; index++)
        {
            var current = HoleLayout[index];
            if (!IsSameHole(current, orderedHole))
            {
                continue;
            }

            if (current.IsManual)
            {
                HoleLayout.RemoveAt(index);
            }
            else
            {
                HoleLayout[index] = current with
                {
                    Number = 0,
                    IsEnabled = false,
                    HasHold = false,
                    HasEstimatedHoldMetadata = true,
                    SourceKind = WallHoleSourceKind.Suppressed
                };
            }

            AssignLedIndicesByWallRouting();
            return;
        }
    }

    public void RestoreSuppressedGeneratedHoles(string panelName)
    {
        for (var index = 0; index < HoleLayout.Count; index++)
        {
            var current = HoleLayout[index];
            if (!string.Equals(current.PanelName, panelName, StringComparison.Ordinal) || !current.IsSuppressed)
            {
                continue;
            }

            HoleLayout[index] = current with
            {
                Number = 0,
                IsEnabled = true,
                SourceKind = WallHoleSourceKind.Generated
            };
        }

        AssignLedIndicesByWallRouting();
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
                true,
                WallHoleSourceKind.Generated)))
            .ToList();
    }

    private void AssignMissingGeneratedHardwareMappings()
    {
        var usedLedIndices = HoleLayout
            .Where(hole => hole.IsManual && hole.LedIndex > 0)
            .Select(hole => hole.LedIndex)
            .ToHashSet();
        var usedPointIds = HoleLayout
            .Where(hole => hole.IsManual && !string.IsNullOrWhiteSpace(hole.PointId))
            .Select(hole => hole.PointId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextLedIndex = 1;
        var nextPointNumber = 1;

        for (var index = 0; index < HoleLayout.Count; index++)
        {
            var hole = HoleLayout[index];
            if (hole.IsManual)
            {
                continue;
            }

            var ledIndex = hole.LedIndex;
            if (ledIndex <= 0 || !usedLedIndices.Add(ledIndex))
            {
                while (usedLedIndices.Contains(nextLedIndex))
                {
                    nextLedIndex++;
                }

                ledIndex = nextLedIndex++;
                usedLedIndices.Add(ledIndex);
            }

            var pointId = hole.PointId;
            if (string.IsNullOrWhiteSpace(pointId) || !usedPointIds.Add(pointId))
            {
                do
                {
                    pointId = BuildDefaultPointId(Name, nextPointNumber++);
                }
                while (usedPointIds.Contains(pointId));

                usedPointIds.Add(pointId);
            }

            HoleLayout[index] = hole with
            {
                PointId = pointId,
                LedIndex = ledIndex
            };
        }
    }

    private void AssignLedIndicesByWallRouting()
    {
        var nextLedIndex = 1;
        foreach (var hole in GetHolesInWallLedOrder(HoleLayout))
        {
            ReplaceHoleMetadata(hole with
            {
                PointId = string.IsNullOrWhiteSpace(hole.PointId)
                    ? BuildDefaultPointId(Name, nextLedIndex)
                    : hole.PointId,
                LedIndex = nextLedIndex++
            });
        }
    }

    private void RemoveDuplicateHoles()
    {
        var uniqueHoles = HoleLayout
            .Select((hole, index) => (hole, index))
            .GroupBy(item => BuildHoleMetadataKey(
                item.hole.PanelName,
                item.hole.RelativeX,
                item.hole.RelativeY))
            .Select(group => group.OrderByDescending(item => item.index).First())
            .OrderBy(item => item.index)
            .Select(item => item.hole)
            .ToList();

        if (uniqueHoles.Count == HoleLayout.Count)
        {
            return;
        }

        HoleLayout.Clear();
        foreach (var hole in uniqueHoles)
        {
            HoleLayout.Add(hole with { Number = 0 });
        }
    }

    private IEnumerable<WallHoleDefinition> GetHolesInWallLedOrder(IEnumerable<WallHoleDefinition> holes)
    {
        if (LedVerticalDirection is not LedStartDirection.TopToBottom and not LedStartDirection.BottomToTop)
        {
            throw new InvalidOperationException("La direzione LED della parete deve essere verticale.");
        }

        const double tolerance = 0.0001d;
        var orderedColumns = holes
            .OrderBy(hole => hole.AbsoluteX)
            .ThenBy(hole => hole.AbsoluteY)
            .GroupBy(hole => Math.Round(hole.AbsoluteX / tolerance) * tolerance)
            .OrderBy(group => group.Key)
            .ToList();

        for (var columnIndex = 0; columnIndex < orderedColumns.Count; columnIndex++)
        {
            var firstColumnTopToBottom = LedVerticalDirection == LedStartDirection.TopToBottom;
            var useTopToBottom = columnIndex % 2 == 0
                ? firstColumnTopToBottom
                : !firstColumnTopToBottom;
            var holesInColumn = useTopToBottom
                ? orderedColumns[columnIndex]
                    .OrderBy(hole => hole.AbsoluteY)
                    .ThenBy(hole => hole.PanelName, StringComparer.Ordinal)
                    .ThenBy(hole => hole.RelativeY)
                : orderedColumns[columnIndex]
                    .OrderByDescending(hole => hole.AbsoluteY)
                    .ThenBy(hole => hole.PanelName, StringComparer.Ordinal)
                    .ThenByDescending(hole => hole.RelativeY);

            foreach (var hole in holesInColumn)
            {
                yield return hole;
            }
        }
    }

    private static IEnumerable<WallHoleDefinition> GetHolesInWallNumberOrder(IEnumerable<WallHoleDefinition> holes)
    {
        // Si completa una colonna esterna di pannelli alla volta (6..1, poi
        // 12..7). All'interno di ogni colonna esterna, le colonne dei fori
        // sono una serpentina: basso→alto, alto→basso, e così via.
        const double tolerance = 0.0001d;
        var materialized = holes.ToList();
        var manuallyOrdered = materialized
            .Where(hole => hole.ManualOrder > 0)
            .OrderBy(hole => hole.ManualOrder)
            .ThenBy(hole => hole.AbsoluteX)
            .ThenByDescending(hole => hole.AbsoluteY)
            .ToList();
        // Panel names are labels only: physical PanelX/PanelY define which
        // holes belong together and therefore the wall numbering order.
        var panelGroups = materialized
            .Where(hole => hole.ManualOrder <= 0)
            .GroupBy(hole => new
            {
                PanelX = Math.Round(hole.PanelX / tolerance) * tolerance,
                PanelY = Math.Round(hole.PanelY / tolerance) * tolerance
            })
            .ToList();
        var wallColumns = panelGroups
            .GroupBy(panel => panel.Key.PanelX)
            .OrderBy(column => column.Key)
            .ToList();

        var ordered = new List<WallHoleDefinition>();
        foreach (var wallColumn in wallColumns)
        {
            var panels = wallColumn
                .OrderByDescending(panel => panel.Key.PanelY)
                .ThenBy(panel => panel.Key.PanelX)
                .Select(panel => new
                {
                    PanelY = panel.Key.PanelY,
                    Columns = panel
                        .GroupBy(hole => Math.Round(hole.RelativeX / tolerance) * tolerance)
                        .OrderBy(column => column.Key)
                        .Select(column => column.ToList())
                        .ToList()
                })
                .ToList();
            var internalColumnCount = panels.Count == 0 ? 0 : panels.Max(panel => panel.Columns.Count);

            for (var internalColumn = 0; internalColumn < internalColumnCount; internalColumn++)
            {
                var bottomToTop = internalColumn % 2 == 0;
                var orderedPanels = bottomToTop
                    ? panels.OrderByDescending(panel => panel.PanelY)
                    : panels.OrderBy(panel => panel.PanelY);

                foreach (var panel in orderedPanels)
                {
                    if (internalColumn >= panel.Columns.Count) continue;
                    ordered.AddRange(bottomToTop
                        ? panel.Columns[internalColumn].OrderByDescending(hole => hole.RelativeY)
                        : panel.Columns[internalColumn].OrderBy(hole => hole.RelativeY));
                }
            }
        }

        return manuallyOrdered.Concat(ordered);
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

    private static double RoundCoordinate(double value)
    {
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static bool IsSameHole(WallHoleDefinition left, WallHoleDefinition right)
    {
        return string.Equals(left.PanelName, right.PanelName, StringComparison.Ordinal)
            && Math.Abs(left.RelativeX - right.RelativeX) < 0.0001d
            && Math.Abs(left.RelativeY - right.RelativeY) < 0.0001d;
    }

    private static string BuildDefaultPointId(string wallName, int holeNumber)
    {
        var safeWallName = string.IsNullOrWhiteSpace(wallName)
            ? "wall"
            : wallName.Trim().ToLowerInvariant().Replace(' ', '-');
        return $"{safeWallName}-hole-{holeNumber:000}";
    }
}
