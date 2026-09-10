using System.Text.Json.Serialization;

namespace CoppAddresd.Application.DTOs.LabExam;

/// <summary>
/// Métrica individual extraída por el AI Service desde un examen de laboratorio.
/// </summary>
public record LabExamAiMetricDto(
    [property: JsonPropertyName("metric_name")] string MetricName,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("unit_symbol")] string UnitSymbol,
    [property: JsonPropertyName("observed_at")] DateTime? ObservedAt = null);

/// <summary>
/// Respuesta del AI Service para la extracción de exámenes de laboratorio.
/// </summary>
public record LabExamAiResponse(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("metrics")] IReadOnlyList<LabExamAiMetricDto> Metrics,
    [property: JsonPropertyName("readable")] bool Readable = true);

/// <summary>
/// Resultado final del procesamiento y persistencia de un examen de laboratorio.
/// </summary>
public record LabExamUploadResult(
    Guid BatchId,
    string Summary,
    int MeasurementCount,
    IReadOnlyList<string> DetectedMetrics,
    string? StorageKey = null);
