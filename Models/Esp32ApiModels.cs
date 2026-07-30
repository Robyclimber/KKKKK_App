using System.Text.Json.Serialization;

namespace WallPanelPlanner.Models;

public sealed class Esp32ApiResponse<T>
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? ErrorCode { get; init; }

    public T? Data { get; init; }

    public string ApiVersion { get; init; } = "1.0";
}

public sealed class Esp32HealthData
{
    public string Status { get; init; } = string.Empty;

    public string FirmwareVersion { get; init; } = string.Empty;
}

public sealed class Esp32StatusData
{
    public string DeviceName { get; init; } = string.Empty;

    public string FirmwareVersion { get; init; } = string.Empty;

    public string RuntimeState { get; init; } = string.Empty;

    public string WifiStatus { get; init; } = string.Empty;

    public string? Ssid { get; init; }

    public string? IpAddress { get; init; }

    public long UptimeSeconds { get; init; }

    public string? ConfiguredWallId { get; init; }

    public string ConfigStatus { get; init; } = string.Empty;

    public int CircuitsCount { get; init; }

    public string CircuitsStatus { get; init; } = string.Empty;

    public string? ActiveCircuitId { get; init; }

    public string LedStatus { get; init; } = string.Empty;

    public string LastCommand { get; init; } = string.Empty;

    public string? LastError { get; init; }
}

public sealed class Esp32SimpleResultData
{
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; init; }
}
