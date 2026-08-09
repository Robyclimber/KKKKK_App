using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RouteLab.Models;

namespace RouteLab.Services;

public sealed class Esp32ApiClient : IEsp32ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<Esp32ApiResponse<Esp32HealthData>> GetHealthAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32HealthData>(settings, HttpMethod.Get, "health", null, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32StatusData>> GetStatusAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32StatusData>(settings, HttpMethod.Get, "status", null, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32CircuitsCatalogData>> GetCircuitsAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32CircuitsCatalogData>(settings, HttpMethod.Get, "circuits", null, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32EditorialCircuitsCatalogData>> GetEditorialCircuitsAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32EditorialCircuitsCatalogData>(settings, HttpMethod.Get, "circuits/editorial", null, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> PostConfigAsync(Esp32DeviceSettings settings, Esp32WallConfigPayload payload, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "config", payload, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> PostCircuitsAsync(Esp32DeviceSettings settings, Esp32CircuitsPayload payload, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuits", payload, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> PostEditorialCircuitsAsync(Esp32DeviceSettings settings, Esp32EditorialCircuitsPayload payload, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuits/editorial", payload, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> VisualizeCircuitAsync(Esp32DeviceSettings settings, string circuitId, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuit/visualize", new Esp32CircuitCommandRequest
        {
            CircuitId = circuitId
        }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> StartCircuitAsync(Esp32DeviceSettings settings, string circuitId, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuit/start", new Esp32CircuitCommandRequest
        {
            CircuitId = circuitId
        }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> ShowCircuitAsync(Esp32DeviceSettings settings, string circuitId, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuit/show", new Esp32CircuitCommandRequest
        {
            CircuitId = circuitId
        }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> StopCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuit/stop", new { }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> ResetCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuit/reset", new { }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearCircuitAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "circuit/clear", new { }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> StartRandomSequenceTestAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "test/random-sequence", new { }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> StartAllLedsTestAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "test/all-leds", new Esp32AllLedsTestRequest
        {
            LedCount = settings.WallLedCount,
            Brightness = settings.BrightnessLimit,
            Color = "#FFFFFF"
        }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> SimulatePointAsync(Esp32DeviceSettings settings, string pointId, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "test/simulate-point", new { PointId = pointId }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> SetSignTextAsync(Esp32DeviceSettings settings, string text, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "sign/text", new { Text = text }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> StartRestFeedbackAsync(Esp32DeviceSettings settings, Esp32RestFeedbackStartRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/rest/start", request, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> CompleteRestFeedbackAsync(Esp32DeviceSettings settings, Esp32RestFeedbackCompleteRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/rest/complete", request, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearRestFeedbackAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/rest/clear", new { }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> StartResistanceFeedbackAsync(Esp32DeviceSettings settings, Esp32ResistanceFeedbackStartRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/resistance/start", request, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> CompleteResistanceFeedbackAsync(Esp32DeviceSettings settings, Esp32ResistanceFeedbackCompleteRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/resistance/complete", request, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearResistanceFeedbackAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/resistance/clear", new { }, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> StartHangFeedbackAsync(Esp32DeviceSettings settings, Esp32HangFeedbackStartRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/hang/start", request, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> CompleteHangFeedbackAsync(Esp32DeviceSettings settings, Esp32HangFeedbackCompleteRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/hang/complete", request, cancellationToken);
    }

    public Task<Esp32ApiResponse<Esp32SimpleResultData>> ClearHangFeedbackAsync(Esp32DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        return SendAsync<Esp32SimpleResultData>(settings, HttpMethod.Post, "training/hang/clear", new { }, cancellationToken);
    }

    private static async Task<Esp32ApiResponse<T>> SendAsync<T>(Esp32DeviceSettings settings, HttpMethod method, string relativePath, object? payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var client = new HttpClient
        {
            BaseAddress = BuildBaseUri(settings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(12)
        };

        using var request = new HttpRequestMessage(method, relativePath);
        if (payload is not null)
        {
            request.Content = BuildJsonContent(payload);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Esp32ApiResponse<T>? result;
        try
        {
            result = JsonSerializer.Deserialize<Esp32ApiResponse<T>>(body, JsonOptions);
        }
        catch (JsonException)
        {
            result = null;
        }

        if (result is not null)
        {
            return result;
        }

        if (response.IsSuccessStatusCode)
        {
            return new Esp32ApiResponse<T>
            {
                Success = true,
                Message = string.IsNullOrWhiteSpace(body) ? "ok" : body,
                ApiVersion = "1.0"
            };
        }

        return new Esp32ApiResponse<T>
        {
            Success = false,
            Message = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body,
            ErrorCode = $"HTTP_{(int)response.StatusCode}",
            ApiVersion = "1.0"
        };
    }

    private static Uri BuildBaseUri(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Inserisci il Base URL di RouteLab Hub.");
        }

        var normalized = baseUrl.Trim().TrimEnd('/');
        if (!normalized.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{normalized}/api";
        }

        if (!normalized.EndsWith('/'))
        {
            normalized = $"{normalized}/";
        }

        return new Uri(normalized, UriKind.Absolute);
    }

    private static HttpContent BuildJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };
        return content;
    }
}
