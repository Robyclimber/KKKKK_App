using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IEsp32ApiClient
{
    Task<Esp32ApiResponse<Esp32HealthData>> GetHealthAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32StatusData>> GetStatusAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32CircuitsCatalogData>> GetCircuitsAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32EditorialCircuitsCatalogData>> GetEditorialCircuitsAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> PostConfigAsync(Esp32DeviceSettings settings, Esp32WallConfigPayload payload, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> PostCircuitsAsync(Esp32DeviceSettings settings, Esp32CircuitsPayload payload, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> PostEditorialCircuitsAsync(Esp32DeviceSettings settings, Esp32EditorialCircuitsPayload payload, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> VisualizeCircuitAsync(Esp32DeviceSettings settings, string circuitId, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> StartCircuitAsync(Esp32DeviceSettings settings, string circuitId, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ShowCircuitAsync(Esp32DeviceSettings settings, string circuitId, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> StopCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ResetCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> StartRandomSequenceTestAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> StartRestFeedbackAsync(Esp32DeviceSettings settings, Esp32RestFeedbackStartRequest request, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> CompleteRestFeedbackAsync(Esp32DeviceSettings settings, Esp32RestFeedbackCompleteRequest request, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearRestFeedbackAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> StartResistanceFeedbackAsync(Esp32DeviceSettings settings, Esp32ResistanceFeedbackStartRequest request, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> CompleteResistanceFeedbackAsync(Esp32DeviceSettings settings, Esp32ResistanceFeedbackCompleteRequest request, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearResistanceFeedbackAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> StartHangFeedbackAsync(Esp32DeviceSettings settings, Esp32HangFeedbackStartRequest request, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> CompleteHangFeedbackAsync(Esp32DeviceSettings settings, Esp32HangFeedbackCompleteRequest request, CancellationToken cancellationToken = default);

    Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearHangFeedbackAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default);
}
