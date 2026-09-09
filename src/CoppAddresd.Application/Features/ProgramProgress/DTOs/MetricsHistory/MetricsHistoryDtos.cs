using System.Text.Json.Serialization;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;

// ===========================================================================
// metrics-history — Historial de métricas clínicas del paciente (Home móvil).
// Datos REALES de app.clinical_measurements (nada fabricado en el cliente).
// Privacidad: clave de caché por paciente; la respuesta solo contiene datos
// del propio paciente. Wire camelCase (convención me/*).
// ===========================================================================

/// <summary>
/// Respuesta de <c>GET /program/me/metrics-history</c>: talla del perfil
/// (para el fallback de IMC) + una entrada por código SOLICITADO que tenga
/// filas en la ventana (códigos sin datos se omiten — el cliente renderiza
/// requires-data).
/// </summary>
public sealed record MetricsHistoryResponseDto(
    [property: JsonPropertyName("heightCm")] decimal? HeightCm,
    [property: JsonPropertyName("metrics")] IReadOnlyList<MetricsHistoryMetricDto> Metrics);

/// <summary>
/// Serie de una métrica: unidad del catálogo, rango de referencia (target),
/// dirección favorable y puntos por fecha local (uno por día, el más reciente
/// gana). <c>target</c> null cuando no hay rango activo; <c>favorableDirection</c>
/// null cuando no hay línea base ni derivación por rango.
/// </summary>
public sealed record MetricsHistoryMetricDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("target")] MetricsHistoryTargetDto? Target,
    [property: JsonPropertyName("favorableDirection")] string? FavorableDirection,
    [property: JsonPropertyName("points")] IReadOnlyList<MetricsHistoryPointDto> Points);

/// <summary>Límites del rango normal (null = sin límite en ese extremo).</summary>
public sealed record MetricsHistoryTargetDto(
    [property: JsonPropertyName("lo")] decimal? Lo,
    [property: JsonPropertyName("hi")] decimal? Hi);

/// <summary>Punto de la serie: fecha local ISO (yyyy-MM-dd) + valor decimal.</summary>
public sealed record MetricsHistoryPointDto(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("value")] decimal Value);

// ===========================================================================
// Payload de CACHÉ (contexto completo por paciente, sin codes/days en la
// clave): la serie COMPLETA de TODAS las métricas activas del catálogo sobre
// la ventana MÁXIMA (365d) + talla + targets + direcciones. El recorte a los
// códigos/días de cada request se aplica DESPUÉS del caché (precedente
// scores-history) — nunca se cachea la respuesta por-petición.
// ===========================================================================

/// <summary>
/// Contexto completo cacheado del paciente (clave <c>metrics-history:{{patientId}}:v1</c>,
/// TTL 5 min): <c>WindowEnd</c> = última fecha local de la ventana cacheada
/// (el hoy local del cómputo) y la serie de TODAS las métricas ACTIVAS del
/// catálogo (incl. weight para el fallback de IMC). El handler recorta por
/// request: valida códigos (400 en CADA request), recorta días y aplica el
/// fallback de IMC — nada de eso se cachea.
/// </summary>
public sealed record MetricsHistoryCacheDto(
    decimal? HeightCm,
    DateOnly WindowEnd,
    IReadOnlyList<MetricsHistoryCacheMetricDto> Metrics);

/// <summary>
/// Serie completa cacheada de una métrica (un punto por fecha local, la más
/// reciente gana; SOLO filas en la unidad por defecto del catálogo — series
/// unit-consistentes). El <c>Code</c> es SIEMPRE el del catálogo (nunca el
/// casing del request).
/// </summary>
public sealed record MetricsHistoryCacheMetricDto(
    string Code,
    string? Unit,
    MetricsHistoryTargetDto? Target,
    string? FavorableDirection,
    IReadOnlyList<MetricsHistoryPointDto> Points);

// ===========================================================================
// Contexto crudo del repositorio (nunca wire): el handler ensambla el caché.
// ===========================================================================

/// <summary>
/// Contexto del historial (resultado del repositorio): talla del perfil,
/// catálogo ACTIVO COMPLETO, mediciones de TODAS las métricas en la ventana
/// MÁXIMA (365d), rangos activos y líneas base del paciente.
/// <c>WindowEnd</c> = hoy local del paciente al computar (el tope de la
/// ventana). Null sin inscripción activa (backstop del 404; el check
/// pre-caché lo revalida en cada request).
/// </summary>
public sealed record MetricsHistoryContext(
    decimal? HeightCm,
    DateOnly WindowEnd,
    IReadOnlyList<MetricsHistoryMetricRow> Metrics,
    IReadOnlyList<MetricsHistoryMeasurementRow> Measurements,
    IReadOnlyList<MetricsHistoryRangeRow> Ranges,
    IReadOnlyList<MetricsHistoryBaselineRow> Baselines);

/// <summary>Métrica del catálogo con su unidad por defecto (ej. bmi → kg_m2).</summary>
public sealed record MetricsHistoryMetricRow(
    Guid MetricId,
    string Code,
    string? UnitCode);

/// <summary>
/// Medición cruda: métrica, fecha LOCAL del paciente, valor, unidad REAL de la
/// fila (para el guard de unidades del fallback/serie) e identidad (desempate).
/// </summary>
public sealed record MetricsHistoryMeasurementRow(
    Guid MetricId,
    DateOnly LocalDate,
    decimal Value,
    string UnitCode,
    DateTime ObservedAt,
    Guid Id);

/// <summary>Rango de referencia activo de una métrica (prioridad + Id para el desempate determinista).</summary>
public sealed record MetricsHistoryRangeRow(
    Guid MetricId,
    decimal? Lo,
    decimal? Hi,
    int Priority,
    Guid Id);

/// <summary>Dirección favorable de la línea base clínica del paciente (SPEC §13.1.2).</summary>
public sealed record MetricsHistoryBaselineRow(
    Guid MetricId,
    FavorableDirection Direction);