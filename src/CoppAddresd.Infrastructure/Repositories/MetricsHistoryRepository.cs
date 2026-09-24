using CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetMetricsHistory;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio del historial de métricas clínicas del paciente
/// (metrics-history): contexto crudo COMPLETO — talla del perfil, catálogo
/// ACTIVO completo, mediciones de TODAS las métricas en la ventana MÁXIMA
/// (365d paciente-local, convertida a UTC DST-aware), rangos activos y
/// líneas base. Clase enfocada de SOLO LECTURA (precedente:
/// <see cref="ScoresHistoryRepository"/> y <see cref="LeagueRepository"/>).
/// Queries set-based acotadas, AsNoTracking, sin N+1. El contexto se CACHEA
/// por paciente sin codes/days; el recorte por-request vive en el handler.
/// </summary>
public sealed class MetricsHistoryRepository(AppDbContext dbContext) : IMetricsHistoryRepository
{
    /// <inheritdoc />
    public async Task<bool> HasActiveEnrollmentAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .ProgramEnrollments.AsNoTracking()
            .AnyAsync(
                e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active,
                ct
            );

    /// <inheritdoc />
    public async Task<MetricsHistoryContext?> GetMetricsHistoryContextAsync(
        Guid patientId,
        CancellationToken ct = default
    )
    {
        var enrollment = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Timezone })
            .FirstOrDefaultAsync(ct);
        if (enrollment is null)
        {
            return null;
        }

        // La ventana es paciente-local (zona de la inscripción, DST-aware).
        return await FetchContextCoreAsync(patientId, enrollment.Timezone, ct);
    }

    /// <inheritdoc />
    public async Task<MetricsHistoryContext> GetOpenMetricsHistoryContextAsync(
        Guid patientId,
        CancellationToken ct = default
    )
    {
        // Sin inscripción: ventana en UTC (el endpoint abierto
        // /api/v1/me/metrics-history no exige inscripción activa; el hoy local
        // es el día UTC y la conversión DST-aware es identidad).
        return await FetchContextCoreAsync(patientId, "UTC", ct);
    }

    /// <summary>
    /// Núcleo compartido del contexto: catálogo ACTIVO completo, mediciones de
    /// TODAS las métricas en la ventana MÁXIMA, rangos activos, líneas base y
    /// talla del perfil. Solo cambia la zona de la ventana (inscripción vs
    /// UTC); las queries son las mismas (set-based, AsNoTracking, sin N+1).
    /// </summary>
    private async Task<MetricsHistoryContext> FetchContextCoreAsync(
        Guid patientId,
        string timezone,
        CancellationToken ct
    )
    {
        var tz = ResolveTimeZone(timezone);
        var todayLocal = PatientLocalToday(timezone);
        var fromLocal = todayLocal.AddDays(-(GetMetricsHistoryQuery.CacheWindowDays - 1));
        var fromUtc = LocalDateToUtcStart(fromLocal, tz);
        var toExclusiveUtc = LocalDateToUtcStart(todayLocal.AddDays(1), tz);

        // Catálogo ACTIVO COMPLETO (la whitelist del handler se valida contra
        // esta lista: la lista de "códigos válidos" SIEMPRE está completa).
        var metricsRaw = await dbContext
            .MeasurementMetrics.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new
            {
                m.Id,
                m.Code,
                DefaultUnitCode = m.DefaultUnit != null ? m.DefaultUnit.Code : null,
            })
            .OrderBy(m => m.Code)
            .ToListAsync(ct);

        var metrics = metricsRaw
            .Select(m => new MetricsHistoryMetricRow(m.Id, m.Code, m.DefaultUnitCode))
            .ToList();

        if (metrics.Count == 0)
        {
            return new MetricsHistoryContext(null, todayLocal, [], [], [], []);
        }

        var metricIds = metrics.Select(m => m.MetricId).ToList();

        // Mediciones de TODAS las métricas en la ventana máxima (UTC,
        // DST-aware), ASC (ObservedAt, Id) → la última del día gana en el
        // ensamblado. Se proyecta la UNIDAD REAL de la fila: el guard de
        // unidades (solo la unidad por defecto del catálogo) vive en el
        // ensamblado (fallback de IMC asume kg por esta misma razón).
        var measurements = await dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Join(
                dbContext.MeasurementMetrics.AsNoTracking(),
                x => x.MetricId,
                m => m.Id,
                (x, m) =>
                    new
                    {
                        x.PatientId,
                        x.MetricId,
                        x.Value,
                        x.ObservedAt,
                        x.Id,
                        x.UnitId,
                        Code = m.Code,
                    }
            )
            .Join(
                dbContext.UnitOfMeasures.AsNoTracking(),
                x => x.UnitId,
                u => u.Id,
                (x, u) =>
                    new
                    {
                        x.PatientId,
                        x.MetricId,
                        x.Value,
                        x.ObservedAt,
                        x.Id,
                        x.Code,
                        UnitCode = u.Code,
                    }
            )
            .Where(x =>
                x.PatientId == patientId
                && metricIds.Contains(x.MetricId)
                && x.ObservedAt >= fromUtc
                && x.ObservedAt < toExclusiveUtc
            )
            .OrderBy(x => x.ObservedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var measurementRows = measurements
            .Select(x => new MetricsHistoryMeasurementRow(
                x.MetricId,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(x.ObservedAt, tz)),
                x.Value,
                x.UnitCode,
                x.ObservedAt,
                x.Id
            ))
            .ToList();

        // Rangos ACTIVOS ordenados en SQL (prioridad DESC, Id ASC — desempate
        // determinista del ensamblado; el ORDER BY del SQL evita dependencia
        // del scan order).
        var ranges = await dbContext
            .MeasurementReferenceRanges.AsNoTracking()
            .Where(r => r.IsActive && metricIds.Contains(r.MetricId))
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id)
            .Select(r => new MetricsHistoryRangeRow(
                r.MetricId,
                r.MinValue,
                r.MaxValue,
                r.Priority,
                r.Id
            ))
            .ToListAsync(ct);

        // Líneas base clínicas del paciente (dirección favorable, SPEC §13.1.2).
        var baselines = await dbContext
            .ClinicalBaselines.AsNoTracking()
            .Where(b => b.PatientId == patientId && metricIds.Contains(b.MetricId))
            .Select(b => new MetricsHistoryBaselineRow(b.MetricId, b.FavorableDirection))
            .ToListAsync(ct);

        // Talla del perfil (obtenida del registro más reciente de signos vitales).
        var heightCm = await dbContext
            .VitalSigns.AsNoTracking()
            .Where(v => v.PatientId == patientId && v.HeightCm != null)
            .OrderByDescending(v => v.MeasuredAt)
            .Select(v => (decimal?)v.HeightCm)
            .FirstOrDefaultAsync(ct);

        return new MetricsHistoryContext(
            heightCm,
            todayLocal,
            metrics,
            measurementRows,
            ranges,
            baselines
        );
    }

    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>Fecha local del paciente (espejo del helper de ProgramRepository).</summary>
    private static DateOnly PatientLocalToday(string timezone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.Date);
        }
    }

    /// <summary>
    /// Inicio UTC de una fecha local (medianoche local, DST-aware).
    /// Edge documentado (zonas clase Santiago, p. ej. America/Santiago): en el
    /// día de la transición DST la medianoche local puede NO EXISTIR (el reloj
    /// salta de 23:59 a 01:00) → ConvertTimeToUtc desplaza el inicio de la
    /// ventana ±1h; el efecto es un punto de borde de la ventana con ±1h de
    /// desvío en UN día al año — aceptado (ventana de 365d; sin impacto
    /// clínico material).
    /// </summary>
    private static DateTime LocalDateToUtcStart(DateOnly date, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }
}
