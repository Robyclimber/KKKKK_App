namespace WallPanelPlanner.Models;

public sealed class Esp32WallConfigPayload
{
    public string WallId { get; init; } = string.Empty;

    public string WallName { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string RoomName { get; init; } = string.Empty;

    public string ControllerId { get; init; } = string.Empty;

    public int LedCount { get; init; }

    public int BrightnessLimit { get; init; }

    public List<Esp32LedPointPayload> Points { get; init; } = new();
}

public sealed class Esp32LedPointPayload
{
    public string PointId { get; init; } = string.Empty;

    public int? HoleNumber { get; init; }

    public string? PanelName { get; init; }

    public int LedIndex { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public bool Enabled { get; init; }

    public string Kind { get; init; } = "hole";
}

public sealed class Esp32CircuitsPayload
{
    public string WallId { get; init; } = string.Empty;

    public bool ReplaceAll { get; init; } = true;

    public List<Esp32CircuitPayload> Circuits { get; init; } = new();
}

public sealed class Esp32CircuitPayload
{
    public string CircuitId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string WallId { get; init; } = string.Empty;

    public string Difficulty { get; init; } = string.Empty;

    public string Inclination { get; init; } = string.Empty;

    public Esp32VisualStylePayload Style { get; init; } = new();

    public List<Esp32CircuitItemPayload> Items { get; init; } = new();

    public List<Esp32CircuitStepPayload> Steps { get; init; } = new();
}

public sealed class Esp32VisualStylePayload
{
    public string DefaultColor { get; init; } = "#00FF00";

    public int Brightness { get; init; } = 96;

    public string Effect { get; init; } = "steady";
}

public sealed class Esp32CircuitItemPayload
{
    public string PointId { get; init; } = string.Empty;

    public string Role { get; init; } = "normal";

    public string Color { get; init; } = "#00FF00";

    public string Effect { get; init; } = "steady";

    public bool Enabled { get; init; } = true;
}

public sealed class Esp32CircuitStepPayload
{
    public string PointId { get; init; } = string.Empty;

    public int OrderIndex { get; init; }

    public int BlinkCount { get; init; }

    public int BlinkPeriodMs { get; init; }

    public int HighlightBrightness { get; init; }

    public int HoldDurationMs { get; init; }

    public int DimmedBrightness { get; init; }

    public string HighlightColor { get; init; } = "#FFFF00";

    public string DimmedColor { get; init; } = "#404000";

    public bool AutoAdvance { get; init; } = true;

    public bool Enabled { get; init; } = true;
}

public sealed class Esp32CircuitCommandRequest
{
    public string CircuitId { get; init; } = string.Empty;
}
