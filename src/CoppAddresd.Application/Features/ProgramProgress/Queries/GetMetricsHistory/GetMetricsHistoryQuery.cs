using CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetMetricsHistory;

/// <summary>
/// Historial de métricas clínicas del paciente autenticado (Home del móvil,
/// metrics-history): serie REAL por fecha local de
/// <c>app.clinical_measurements</c> para los códigos solicitados, con rango
/// de referencia (target) y dirección favorable. Nada fabricado en el
/// cliente; SOLO filas persistidas — nunca dispara el motor de puntajes.
///
/// El <c>patientId</c> SIEMPRE llega resuelto de la identidad del JWT por la
/// capa API (nunca del body, anti-IDOR AC-11): sin inscripción activa → 404
/// <c>NO_ACTIVE_ENROLLMENT</c> (verificado en CADA request, fuera del caché).
///
/// Cache (precedente scores-history): el CONTEXTO COMPLETO del paciente se
/// cachea por clave <c>metrics-history:{{patientId}}:v1</c> (TTL 5 min,
/// fail-open) SIN codes/days — la serie de TODAS las métricas activas sobre
/// la ventana máxima. El recorte por-request (whitelist → 400 en CADA
/// request, clamp de días, re-filtro de la ventana, fallback de IMC) ocurre
/// DESPUÉS del caché y nunca se cachea.
/// </summary>
public sealed record GetMetricsHistoryQuery(
    Guid PatientId,
    IReadOnlyList<string> Codes,
    int Days) : IRequest<MetricsHistoryResponseDto>
{
    /// <summary>Códigos por defecto cuando el cliente no envía <c>codes</c>.</summary>
    public static readonly string[] DefaultCodes = ["bmi", "hba1c", "body_fat"];

    public const int DefaultDays = 180;

    public const int MinDays = 7;

    public const int MaxDays = 365;

    /// <summary>Ventana MÁXIMA del contexto cacheado (el recorte por-request nunca excede este rango).</summary>
    public const int CacheWindowDays = 365;
}

public sealed class GetMetricsHistoryQueryHandler(
    IMetricsHistoryRepository repository,
    ICacheService cache,
    ILogger<GetMetricsHistoryQueryHandler> logger) : IRequestHandler<GetMetricsHistoryQuery, MetricsHistoryResponseDto>
{
    /// <summary>
    /// Métricas donde estar BAJO el rango normal es mejor (derivación de
    /// favorableDirection cuando no hay línea base clínica). Documentada en
    /// README §2.13. El resto de métricas → null sin línea base.
    /// </summary>
    private static readonly IReadOnlySet<string> LowerIsBetterMetrics =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bmi", "hba1c", "body_fat", "glucose_fasting",
        };

    public async Task<MetricsHistoryResponseDto> Handle(
        GetMetricsHistoryQuery request, CancellationToken ct)
    {
        // 404 PRE-CACHÉ (estado del paciente, no datos): se revalida en CADA
        // request — el 404 nunca se sirve de una clave caliente.
        if (!await repository.HasActiveEnrollmentAsync(request.PatientId, ct))
        {
            throw new NotFoundException(
                $"NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente {request.PatientId}.");
        }

        var days = request.Days <= 0 ? GetMetricsHistoryQuery.DefaultDays : Math.Clamp(request.Days, GetMetricsHistoryQuery.MinDays, GetMetricsHistoryQuery.MaxDays);
        var requested = request.Codes.Count == 0 ? GetMetricsHistoryQuery.DefaultCodes : request.Codes;
        // Higiene de códigos: sin duplicados y sin case-variants (se emite el
        // Code del CATÁLOGO, nunca el string del request).
        var codes = requested.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var cachePayload = await cache.GetOrCreateAsync(
            CacheKeys.MetricsHistory(request.PatientId),
            CacheKeys.MetricsHistoryTtl,
            async token =>
            {
                var context = await repository.GetMetricsHistoryContextAsync(request.PatientId, token)
                    ?? throw new NotFoundException(
                        $"NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente {request.PatientId}.");

                return BuildCachePayload(context);
            },
            ct);

        // Whitelist contra el catálogo ACTIVO COMPLETO (cacheado): la lista de
        // válidos SIEMPRE está completa y el 400 se aplica en CADA request
        // (también con caché caliente).
        var validCodes = cachePayload.Metrics
            .Select(m => m.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = codes.Where(c => !validCodes.Contains(c)).ToList();
        if (unknown.Count > 0)
        {
            var valid = string.Join(",", cachePayload.Metrics.Select(m => m.Code).OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
            throw new ValidationException(
                [
                    new ValidationFailure(
                        "codes",
                        $"METRICS_UNKNOWN: códigos inválidos: {string.Join(",", unknown)}. Códigos válidos: {valid}."),
                ]);
        }

        var response = TrimToRequest(cachePayload, codes, days);

        logger.LogInformation(
            "Program.MetricsHistory: métricas={Count} días={Days}", response.Metrics.Count, days);

        return response;
    }

    /// <summary>
    /// Ensambla el payload CACHEADO (contexto completo, sin codes/days): por
    /// métrica activa del catálogo, serie dedupada por fecha local (la más
    /// reciente gana; empate → mayor Id) SOLO con filas en la unidad por
    /// defecto del catálogo (series unit-consistentes; otras unidades se
    /// omiten — conversión es trabajo futuro), target del rango de mayor
    /// prioridad (desempate determinista por Id) y dirección favorable
    /// (línea base manda; derivación por rango para el set lower-better).
    /// </summary>
    internal static MetricsHistoryCacheDto BuildCachePayload(MetricsHistoryContext context)
    {
        var measurementsByMetric = context.Measurements
            .GroupBy(m => m.MetricId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var rangeByMetric = context.Ranges
            .GroupBy(r => r.MetricId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Priority).ThenBy(r => r.Id).First());
        var directionByMetric = context.Baselines
            .GroupBy(b => b.MetricId)
            .ToDictionary(g => g.Key, g => g.First().Direction);

        var metrics = context.Metrics
            .OrderBy(m => m.Code, StringComparer.OrdinalIgnoreCase)
            .Select(
                metric =>
                {
                    // Guard de unidad: solo filas en la unidad por defecto del
                    // catálogo (serie unit-consistente; el fallback de IMC
                    // asume kg por esta misma razón — documentado).
                    var rows = (measurementsByMetric.GetValueOrDefault(metric.MetricId) ?? [])
                        .Where(
                            m =>
                                metric.UnitCode is not null
                                && m.UnitCode.Equals(metric.UnitCode, StringComparison.OrdinalIgnoreCase)
                        )
                        .ToList();
                    return new MetricsHistoryCacheMetricDto(
                        metric.Code,
                        metric.UnitCode,
                        rangeByMetric.TryGetValue(metric.MetricId, out var range)
                            ? new MetricsHistoryTargetDto(range.Lo, range.Hi)
                            : null,
                        ResolveFavorableDirection(metric.Code, metric.MetricId, directionByMetric, rangeByMetric),
                        BuildPoints(rows));
                }
            )
            .ToList();

        return new MetricsHistoryCacheDto(context.HeightCm, context.WindowEnd, metrics);
    }

    /// <summary>
    /// Recorte POR-REQUEST (nunca cacheado): valida códigos (ya hecha por el
    /// caller), re-filtra cada serie a la ventana solicitada
    /// [WindowEnd - (days - 1), WindowEnd] y aplica el fallback de IMC
    /// (peso + talla) cuando bmi no tiene filas en la ventana. Emite el Code
    /// del CATÁLOGO. Códigos solicitados sin filas → omitidos (requires-data).
    /// </summary>
    internal static MetricsHistoryResponseDto TrimToRequest(
        MetricsHistoryCacheDto cache,
        IReadOnlyList<string> codes,
        int days)
    {
        var windowEnd = cache.WindowEnd;
        var from = windowEnd.AddDays(-(days - 1));
        var byCode = cache.Metrics.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);

        var metrics = new List<MetricsHistoryMetricDto>();
        foreach (var code in codes)
        {
            if (!byCode.TryGetValue(code, out var metric))
            {
                continue; // whitelist ya validada; defensivo
            }

            var points = metric.Points
                .Where(p => p.Date >= from && p.Date <= windowEnd)
                .ToList();

            if (
                points.Count == 0
                && code.Equals("bmi", StringComparison.OrdinalIgnoreCase)
                && byCode.TryGetValue("weight", out var weight)
                && cache.HeightCm is { } heightCm
                && heightCm > 0
            )
            {
                // FALLBACK IMC (única métrica computada, documentado): sin
                // filas de bmi en la ventana pero con peso (serie cacheada ya
                // filtrada a la unidad kg del catálogo) + talla del perfil →
                // bmi por fecha = peso/(talla/100)².
                var weightPoints = weight.Points
                    .Where(p => p.Date >= from && p.Date <= windowEnd && p.Value > 0)
                    .ToList();
                var heightM = heightCm / 100m;
                points = weightPoints
                    .Select(p => new MetricsHistoryPointDto(p.Date, Math.Round(p.Value / (heightM * heightM), 2)))
                    .ToList();
            }

            if (points.Count == 0)
            {
                continue; // sin datos en la ventana → el código se omite
            }

            metrics.Add(
                new MetricsHistoryMetricDto(
                    metric.Code,
                    metric.Unit,
                    metric.Target,
                    metric.FavorableDirection,
                    points)
            );
        }

        return new MetricsHistoryResponseDto(cache.HeightCm, metrics);
    }

    /// <summary>
    /// Un punto por fecha local: las filas llegan ASC (ObservedAt, Id) y la
    /// última del grupo gana (la más reciente; el Id desempata empates
    /// exactos de ObservedAt).
    /// </summary>
    private static List<MetricsHistoryPointDto> BuildPoints(
        IReadOnlyList<MetricsHistoryMeasurementRow> measurements) =>
        measurements
            .GroupBy(m => m.LocalDate)
            .Select(g => new MetricsHistoryPointDto(g.Key, g.Last().Value))
            .OrderBy(p => p.Date)
            .ToList();

    /// <summary>
    /// Dirección favorable: la línea base clínica (autoría clínica, SPEC
    /// §13.1.2) manda; sin línea base, se deriva del rango SOLO para el set
    /// lower-better (bmi/hba1c/body_fat/glucose_fasting con rango → 'down');
    /// el resto → null (desconocible).
    /// </summary>
    private static string? ResolveFavorableDirection(
        string code,
        Guid metricId,
        IReadOnlyDictionary<Guid, FavorableDirection> baselines,
        IReadOnlyDictionary<Guid, MetricsHistoryRangeRow> ranges)
    {
        if (baselines.TryGetValue(metricId, out var direction))
        {
            return direction == FavorableDirection.LowerIsBetter ? "down" : "up";
        }

        if (ranges.ContainsKey(metricId) && LowerIsBetterMetrics.Contains(code))
        {
            return "down";
        }

        return null;
    }
}