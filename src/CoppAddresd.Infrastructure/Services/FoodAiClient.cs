using System.Net.Http.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Cliente HTTP del Food AI Service. El probe de salud nunca lanza: un fallo
/// de red, timeout o respuesta inválida se reporta como Unhealthy.
/// </summary>
public sealed class FoodAiClient : IFoodAiClient
{
    private readonly HttpClient _httpClient;
    private readonly FoodAiSettings _settings;
    private readonly ILogger<FoodAiClient> _logger;

    public FoodAiClient(
        HttpClient httpClient,
        IOptions<FoodAiSettings> settings,
        ILogger<FoodAiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<FoodAiHealthStatus> GetHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

            var response = await _httpClient.GetAsync(_settings.HealthEndpoint, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new FoodAiHealthStatus(false, $"HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadFromJsonAsync<FoodAiHealthResponseJson>(
                cancellationToken: timeout.Token);
            return body is { Status: "healthy" }
                ? new FoodAiHealthStatus(true, body.Service)
                : new FoodAiHealthStatus(false, body?.Status ?? "respuesta inválida");
        }
        catch (OperationCanceledException)
        {
            return new FoodAiHealthStatus(false, "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Food AI Service no responde");
            return new FoodAiHealthStatus(false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private sealed class FoodAiHealthResponseJson
    {
        public string? Status { get; set; }
        public string? Service { get; set; }
    }
}