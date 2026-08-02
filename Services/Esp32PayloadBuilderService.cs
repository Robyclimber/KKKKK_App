using System.Text;
using RouteLab.Models;

namespace RouteLab.Services;

public sealed class Esp32PayloadBuilderService : IEsp32PayloadBuilderService
{
    public Esp32WallConfigPayload BuildWallConfig(WallDefinition wall, RoomDefinition room, Esp32DeviceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(wall);
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(settings);

        var orderedHoles = wall.GetOrderedHoles();
        if (orderedHoles.Count == 0)
        {
            throw new InvalidOperationException("La parete non contiene fori da esportare verso RouteLab Hub.");
        }

        if (settings.WallLedCount < orderedHoles.Count)
        {
            throw new InvalidOperationException($"Il numero LED configurato ({settings.WallLedCount}) e' minore dei fori presenti ({orderedHoles.Count}).");
        }

        var wallId = BuildWallId(wall);
        return new Esp32WallConfigPayload
        {
            WallId = wallId,
            WallName = wall.Name,
            RoomId = BuildRoomId(room),
            RoomName = room.Name,
            ControllerId = settings.ControllerId.Trim(),
            LedCount = settings.WallLedCount,
            BrightnessLimit = Math.Clamp(settings.BrightnessLimit, 0, 255),
            Points = orderedHoles
                .Select(hole => new Esp32LedPointPayload
                {
                    PointId = string.IsNullOrWhiteSpace(hole.PointId) ? BuildPointId(wallId, hole.Number) : hole.PointId,
                    HoleNumber = hole.Number,
                    PanelName = hole.PanelName,
                    LedIndex = Math.Max(0, hole.LedIndex - 1),
                    X = hole.AbsoluteX,
                    Y = hole.AbsoluteY,
                    Enabled = hole.IsEnabled,
                    Kind = "hole"
                })
                .ToList()
        };
    }

    public Esp32CircuitsPayload BuildCircuitsPayload(WallDefinition wall, RoomDefinition room, IEnumerable<CircuitDefinition> circuits)
    {
        ArgumentNullException.ThrowIfNull(wall);
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(circuits);

        var wallId = BuildWallId(wall);
        var pointsByNumber = wall.GetOrderedHoles().ToDictionary(hole => hole.Number);

        var payloadCircuits = circuits
            .Where(circuit =>
                string.Equals(circuit.RoomName, room.Name, StringComparison.Ordinal) &&
                circuit.GetWallNames().Count == 1 &&
                circuit.UsesWall(wall.Name))
            .Select(circuit => BuildCircuitPayload(circuit, wall.Name, wallId, pointsByNumber))
            .ToList();

        return new Esp32CircuitsPayload
        {
            WallId = wallId,
            ReplaceAll = true,
            Circuits = payloadCircuits
        };
    }

    private static Esp32CircuitPayload BuildCircuitPayload(
        CircuitDefinition circuit,
        string wallName,
        string wallId,
        IReadOnlyDictionary<int, WallHoleDefinition> pointsByNumber)
    {
        var orderedMovements = circuit.Movements
            .Where(movement => string.Equals(movement.WallName, wallName, StringComparison.Ordinal))
            .OrderBy(movement => movement.IsFootHold ? 0 : 1)
            .ThenBy(movement => movement.Sequence)
            .ThenBy(movement => movement.Hand)
            .ToList();
        var orderedStepMovements = orderedMovements
            .Where(movement => !movement.IsFootHold)
            .ToList();

        return new Esp32CircuitPayload
        {
            CircuitId = BuildCircuitId(circuit),
            Name = circuit.Name,
            WallId = wallId,
            Difficulty = circuit.Difficulty,
            Inclination = circuit.Inclination,
            Style = new Esp32VisualStylePayload
            {
                DefaultColor = string.IsNullOrWhiteSpace(circuit.Globals.RightHandColor) ? "#C44536" : circuit.Globals.RightHandColor,
                Brightness = Math.Clamp(circuit.Globals.DefaultBrightness, 0, 255),
                Effect = string.IsNullOrWhiteSpace(circuit.Globals.Effect) ? "steady" : circuit.Globals.Effect
            },
            Items = orderedMovements
                .Select(movement => new Esp32CircuitItemPayload
                {
                    PointId = ResolvePointId(wallId, movement.HoleNumber, pointsByNumber),
                    Role = BuildRole(movement),
                    Color = BuildItemColor(movement, circuit.Globals),
                    Effect = movement.Role == MovementRole.Top ? "pulse" : "steady",
                    Enabled = true
                })
                .ToList(),
            Steps = orderedStepMovements
                .Select((movement, index) => new Esp32CircuitStepPayload
                {
                    PointId = ResolvePointId(wallId, movement.HoleNumber, pointsByNumber),
                    OrderIndex = index,
                    BlinkCount = movement.Role == MovementRole.Start ? Math.Max(1, circuit.Globals.BlinkCount) : movement.Role == MovementRole.Top ? Math.Max(1, circuit.Globals.BlinkCount) : 1,
                    BlinkPeriodMs = Math.Max(1, circuit.Globals.BlinkPeriodMs),
                    HighlightBrightness = Math.Clamp(circuit.Globals.DefaultBrightness, 0, 255),
                    HoldDurationMs = Math.Max(100, circuit.Globals.HoldDurationMs),
                    DimmedBrightness = Math.Clamp(circuit.Globals.DimmedBrightness, 0, 255),
                    HighlightColor = BuildItemColor(movement, circuit.Globals),
                    DimmedColor = BuildDimmedColor(movement),
                    AutoAdvance = true,
                    Enabled = pointsByNumber.ContainsKey(movement.HoleNumber)
                })
                .ToList()
        };
    }

    private static string ResolvePointId(string wallId, int holeNumber, IReadOnlyDictionary<int, WallHoleDefinition> pointsByNumber)
    {
        if (pointsByNumber.TryGetValue(holeNumber, out var hole) && !string.IsNullOrWhiteSpace(hole.PointId))
        {
            return hole.PointId;
        }

        return BuildPointId(wallId, holeNumber);
    }

    private static string BuildRole(CircuitMovementDefinition movement)
    {
        return movement.Role switch
        {
            MovementRole.Start => "start",
            MovementRole.Top => "top",
            MovementRole.Feet => "foot",
            _ => movement.Hand == HandSide.Right ? "rightHand" : "leftHand"
        };
    }

    private static string BuildItemColor(CircuitMovementDefinition movement)
    {
        return BuildItemColor(movement, null);
    }

    private static string BuildItemColor(CircuitMovementDefinition movement, CircuitGlobalsDefinition? globals)
    {
        return movement.Role switch
        {
            MovementRole.Start => string.IsNullOrWhiteSpace(globals?.StartColor) ? "#FFFF00" : globals!.StartColor,
            MovementRole.Top => string.IsNullOrWhiteSpace(globals?.TopColor) ? "#FF0000" : globals!.TopColor,
            MovementRole.Feet => "#FFFFFF",
            _ => movement.Hand == HandSide.Right
                ? string.IsNullOrWhiteSpace(globals?.RightHandColor) ? "#C44536" : globals!.RightHandColor
                : string.IsNullOrWhiteSpace(globals?.LeftHandColor) ? "#247BA0" : globals!.LeftHandColor
        };
    }

    private static string BuildDimmedColor(CircuitMovementDefinition movement)
    {
        return movement.Role switch
        {
            MovementRole.Start => "#404000",
            MovementRole.Top => "#400000",
            MovementRole.Feet => "#404040",
            _ => movement.Hand == HandSide.Right ? "#40201C" : "#1D3440"
        };
    }

    public static string BuildWallId(WallDefinition wall)
    {
        return $"wall-{wall.Id}-{Slugify(wall.Name)}";
    }

    public static string BuildRoomId(RoomDefinition room)
    {
        return $"room-{room.Id}-{Slugify(room.Name)}";
    }

    public static string BuildCircuitId(CircuitDefinition circuit)
    {
        return string.IsNullOrWhiteSpace(circuit.CircuitId)
            ? $"circuit-{circuit.Id}-{Slugify(circuit.Name)}"
            : circuit.CircuitId;
    }

    public static string BuildPointId(string wallId, int holeNumber)
    {
        return $"{wallId}-hole-{holeNumber:000}";
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unnamed";
        }

        var builder = new StringBuilder(value.Length);
        var previousDash = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousDash = false;
                continue;
            }

            if (previousDash)
            {
                continue;
            }

            builder.Append('-');
            previousDash = true;
        }

        return builder.ToString().Trim('-');
    }
}
