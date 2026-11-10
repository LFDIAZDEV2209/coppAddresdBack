using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoppAddresd.Application.DTOs.LabExam;

/// <summary>
/// Métrica individual extraída por el AI Service desde un examen de laboratorio.
/// </summary>
public record LabExamAiMetricDto(
    [property: JsonPropertyName("metric_name")] string MetricName,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("unit_symbol")] string? UnitSymbol,
    [property: JsonPropertyName("observed_at")] DateTime? ObservedAt = null,
    [property: JsonPropertyName("source_text")] string? SourceText = null,
    [property: JsonPropertyName("source_page")] int? SourcePage = null);

/// <summary>
/// Respuesta del AI Service para la extracción de exámenes de laboratorio.
/// </summary>
public record LabExamAiResponse(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("metrics")] IReadOnlyList<LabExamAiMetricDto> Metrics,
    [property: JsonPropertyName("readable")] bool Readable = true);

/// <summary>
/// Compatibilidad del contrato de narración. Los miembros Improved/Worsened solo
/// permiten leer contratos históricos; las comparaciones nuevas nunca los emiten.
/// </summary>
[JsonConverter(typeof(MetricDirectionJsonConverter))]
public enum MetricDirection
{
    Improved,
    Stable,
    Worsened,
    Changed,
    FirstRecord,
}

/// <summary>
/// Serializa <see cref="MetricDirection"/> con los valores snake_case que espera el
/// ai-service (espejo de <c>PreviousMeasurement.direction</c> en
/// <c>app/api/schemas.py</c>), independientemente de la naming policy del serializer.
/// </summary>
public sealed class MetricDirectionJsonConverter : JsonConverter<MetricDirection>
{
    public override MetricDirection Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "improved" => MetricDirection.Improved,
            "stable" => MetricDirection.Stable,
            "worsened" => MetricDirection.Worsened,
            "changed" => MetricDirection.Changed,
            "first_record" => MetricDirection.FirstRecord,
            var value => throw new JsonException($"Unknown metric direction '{value}'.")
        };

    public override void Write(
        Utf8JsonWriter writer, MetricDirection value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            MetricDirection.Improved => "changed",
            MetricDirection.Stable => "stable",
            MetricDirection.Worsened => "changed",
            MetricDirection.Changed => "changed",
            MetricDirection.FirstRecord => "first_record",
            _ => throw new JsonException($"Unknown metric direction '{value}'.")
        });
}

/// <summary>
/// Evolución pre-computada de una métrica (delta y dirección calculados en C#).
/// Los campos <c>previous_*</c> conservan la observación anterior incluso cuando
/// no es comparable. <c>delta</c> solo se calcula con unidad explícita coincidente
/// y fechas explícitas ordenadas; <c>change</c> no expresa beneficio clínico.
/// </summary>
public sealed record MetricEvolution(
    [property: JsonPropertyName("direction")] MetricDirection Direction,
    [property: JsonPropertyName("previous_value")] decimal? PreviousValue = null,
    [property: JsonPropertyName("previous_unit")] string? PreviousUnit = null,
    [property: JsonPropertyName("previous_date")] DateTime? PreviousDate = null,
    [property: JsonPropertyName("delta")] decimal? Delta = null,
    [property: JsonPropertyName("current_value")] decimal? CurrentValue = null,
    [property: JsonPropertyName("current_unit")] string? CurrentUnit = null,
    [property: JsonPropertyName("change")] string? Change = null,
    [property: JsonPropertyName("current_date")] DateTime? CurrentDate = null,
    [property: JsonPropertyName("previous_source")] string? PreviousSource = null,
    [property: JsonPropertyName("current_source")] string? CurrentSource = null);

/// <summary>
/// Valor puntual de una métrica (código canónico + unidad normalizada) usado como
/// entrada del cálculo de evolución: valores actuales del lote y última medición
/// previa por métrica.
/// </summary>
public sealed record LabExamMetricSnapshot(
    string MetricCode,
    decimal Value,
    string? UnitSymbol,
    DateTime? ObservedAt,
    string? Source = null);

/// <summary>
/// Cuerpo del endpoint de narración empática (<c>POST /chat/lab-exam/narrate</c>).
/// Espejo de <c>NarrateRequest</c> en <c>ai-service/app/api/schemas.py</c>:
/// <c>metrics</c> (métricas del examen actual), <c>previous_measurements</c>
/// (evolución por métrica pre-computada en .NET) y <c>language</c> opcional.
/// </summary>
public sealed record LabExamNarrateRequest(
    [property: JsonPropertyName("metrics")] IReadOnlyList<LabExamAiMetricDto> Metrics,
    [property: JsonPropertyName("previous_measurements")] IReadOnlyDictionary<string, MetricEvolution> PreviousMeasurements,
    [property: JsonPropertyName("language"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Language = null);

/// <summary>
/// Candidato extraído del archivo, sin verificación clínica ni persistencia como medición.
/// La unidad/fecha faltante se conserva en null; nunca se sustituye por un supuesto.
/// </summary>
public sealed record LabExamExtractedMetricDto(
    string MetricName,
    string? MetricCode,
    decimal Value,
    string? UnitSymbol,
    DateTime? ObservedAt,
    string Source = "ocr_unverified",
    string? SourceText = null,
    int? SourcePage = null);

/// <summary>
/// Resultado compatible de la subida. MeasurementCount cuenta mediciones clínicas
/// establecidas, no candidatos OCR. La extracción queda pendiente de verificación.
/// </summary>
public record LabExamUploadResult(
    Guid BatchId,
    string Summary,
    int MeasurementCount,
    IReadOnlyList<string> DetectedMetrics,
    string? StorageKey = null,
    string? VerificationStatus = null,
    Guid? DocumentId = null,
    string? ExtractionStorageKey = null,
    IReadOnlyList<LabExamExtractedMetricDto>? ExtractedMetrics = null,
    IReadOnlyDictionary<string, MetricEvolution>? Comparisons = null);
