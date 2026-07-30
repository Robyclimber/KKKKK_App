using System.Text;
using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

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
            throw new InvalidOperationException("La parete non contiene fori da esportare verso ESP32.");
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
                string.Equals(circuit.WallName, wall.Name, StringComparison.Ordinal))
            .Select(circuit => BuildCircuitPayload(circuit, wallId, pointsByNumber))
            .ToList();

        return new Esp32CircuitsPayload
        {
            WallId = wallId,
            ReplaceAll = true,
            Circuits = payloadCircuits
        };
    }

    private static Esp32CircuitPayload BuildCircuitPayload(CircuitDefinition circuit, string wallId, IReadOnlyDictionary<int, WallHoleDefinition> pointsByNumber)
    {
        var orderedMovements = circuit.Movements
            .OrderBy(movement => movement.Sequence)
            .ThenBy(movement => movement.Hand)
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
                DefaultColor = "#00FF00",
                Brightness = 96,
                Effect = "steady"
            },
            Items = orderedMovements
                .Select(movement => new Esp32CircuitItemPayload
                {
                    PointId = ResolvePointId(wallId, movement.HoleNumber, pointsByNumber),
                    Role = BuildRole(movement),
                    Color = BuildItemColor(movement),
                    Effect = movement.Role == MovementRole.Top ? "pulse" : "steady",
                    Enabled = true
                })
                .ToList(),
            Steps = orderedMovements
                .Select((movement, index) => new Esp32CircuitStepPayload
                {
                    PointId = ResolvePointId(wallId, movement.HoleNumber, pointsByNumber),
                    OrderIndex = index,
                    BlinkCount = movement.Role == MovementRole.Start ? 3 : movement.Role == MovementRole.Top ? 4 : 1,
                    BlinkPeriodMs = movement.Role == MovementRole.Top ? 220 : 280,
                    HighlightBrightness = 242,
                    HoldDurationMs = movement.Role == MovementRole.Top ? 3500 : 2500,
                    DimmedBrightness = 96,
                    HighlightColor = BuildItemColor(movement),
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
            _ => movement.Hand == HandSide.Right ? "rightHand" : "leftHand"
        };
    }

    private static string BuildItemColor(CircuitMovementDefinition movement)
    {
        return movement.Role switch
        {
            MovementRole.Start => "#FFFF00",
            MovementRole.Top => "#FF0000",
            _ => movement.Hand == HandSide.Right ? "#C44536" : "#247BA0"
        };
    }

    private static string BuildDimmedColor(CircuitMovementDefinition movement)
    {
        return movement.Role switch
        {
            MovementRole.Start => "#404000",
            MovementRole.Top => "#400000",
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
        return $"circuit-{circuit.Id}-{Slugify(circuit.Name)}";
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
