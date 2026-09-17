using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

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

    public async Task<FoodAiAnalyzeResult> SendImageAsync(
        Guid analysisId,
        Stream image,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var imageContent = new StreamContent(image);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(imageContent, "image", fileName);
            form.Add(new StringContent(analysisId.ToString()), "analysis_id");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.AnalyzeEndpoint)
            {
                Content = form,
            };

            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Food AI Service rechazó el análisis (status {Status}): {Detail}",
                    (int)response.StatusCode, detail);
                throw new FoodAiException((int)response.StatusCode, detail);
            }

var body = await response.Content.ReadFromJsonAsync<FoodAiAnalyzeResponseJson>(
            JsonOpts, cancellationToken: ct);
        if (body is null || string.IsNullOrWhiteSpace(body.AnalysisId))
        {
            throw new FoodAiException(502, "Respuesta inválida del Food AI Service.");
        }

        return new FoodAiAnalyzeResult(
            body.AnalysisId,
            body.Status ?? "completed",
            body.ModelVersion ?? "unknown",
            body.SegModelVersion ?? "none",
            body.ClassifierVersion ?? "none",
            body.InferenceTimeMs ?? 0,
            (body.Foods ?? []).Select(f => new DetectedFoodDto(
                f.Name,
                f.Confidence,
                new BoundingBoxDto(f.BoundingBox?.X ?? 0, f.BoundingBox?.Y ?? 0, f.BoundingBox?.Width ?? 0, f.BoundingBox?.Height ?? 0),
                f.Segmentation is null
                    ? null
                    : new SegmentationDto(
                        RequireValue(f.Segmentation.Mask),
                        f.Segmentation.AreaPixels),
                f.Portion is null
                    ? null
                    : new PortionDto(
                        RequireValue(f.Portion.PortionSize),
                        f.Portion.EstimatedGrams,
                        f.Portion.MinGrams,
                        f.Portion.MaxGrams,
                        f.Portion.Confidence,
                        RequireValue(f.Portion.Method))))
            .ToList());
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Respuesta JSON inválida del Food AI Service");
            throw new FoodAiException(502, "Respuesta inválida del Food AI Service.");
        }
    }

    private static string RequireValue(string? value) =>
        value ?? throw new FoodAiException(502, "Respuesta inválida del Food AI Service.");

    private sealed class FoodAiAnalyzeResponseJson
    {
        public string? AnalysisId { get; set; }
        public string? Status { get; set; }
        public string? ModelVersion { get; set; }
        public string? SegModelVersion { get; set; }
        public string? ClassifierVersion { get; set; }
        public int? InferenceTimeMs { get; set; }
        public List<DetectedFoodJson>? Foods { get; set; }
    }

    private sealed class DetectedFoodJson
    {
        public string? Name { get; set; }
        public double Confidence { get; set; }
        public BoundingBoxJson? BoundingBox { get; set; }
        public SegmentationJson? Segmentation { get; set; }
        public PortionJson? Portion { get; set; }
    }

    private sealed class PortionJson
    {
        public string? PortionSize { get; set; }
        public int? EstimatedGrams { get; set; }
        public int? MinGrams { get; set; }
        public int? MaxGrams { get; set; }
        public double Confidence { get; set; }
        public string? Method { get; set; }
    }

    private sealed class SegmentationJson
    {
        public string? Mask { get; set; }
        public int AreaPixels { get; set; }
    }

    private sealed class BoundingBoxJson
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private sealed class FoodAiHealthResponseJson
    {
        public string? Status { get; set; }
        public string? Service { get; set; }
    }
}