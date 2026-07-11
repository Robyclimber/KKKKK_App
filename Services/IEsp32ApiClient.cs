using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public interface IEsp32ApiClient
{
    Task<Esp32ApiResponse<Esp32HealthData>> GetHealthAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32StatusData>> GetStatusAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> PostConfigAsync(Esp32DeviceSettings settings, Esp32WallConfigPayload payload, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> PostCircuitsAsync(Esp32DeviceSettings settings, Esp32CircuitsPayload payload, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ShowCircuitAsync(Esp32DeviceSettings settings, string circuitId, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> StopCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ResetCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);
}
