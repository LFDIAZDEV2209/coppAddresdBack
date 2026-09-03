using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seed del catálogo de mediciones clínicas: unidades, métricas y rangos de
/// referencia iniciales (referencia ADA/OMS, a validar clínicamente).
///
/// Idempotente por <c>code</c> (unidades y métricas) y por
/// (métrica, límites, unidad) en los rangos. Cada operación usa un scope propio
/// con su propio DbContext para evitar conflictos de tracking EF, igual que
/// <see cref="AgentCatalogSeeder"/>.
/// </summary>
public sealed class ClinicalMeasurementsSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<ClinicalMeasurementsSeeder> logger) : IHostedService
{
    private sealed record UnitSeed(string Code, string Name, string Symbol);

    private sealed record MetricSeed(string Code, string Name, string Category, string DefaultUnitCode);

    private sealed record ReferenceRangeSeed(
        string MetricCode,
        int? AgeMin,
        int? AgeMax,
        string? Gender,
        decimal? MinValue,
        decimal? MaxValue,
        string UnitCode,
        int Priority);

    private static readonly IReadOnlyList<UnitSeed> Units =
    [
        new("mg_dl", "Miligramos por decilitro", "mg/dL"),
        new("kg", "Kilogramos", "kg"),
        new("cm", "Centímetros", "cm"),
        new("mmhg", "Milímetros de mercurio", "mmHg"),
        new("bpm", "Latidos por minuto", "bpm"),
        new("pct", "Porcentaje", "%"),
        new("kg_m2", "Kilogramos por metro cuadrado", "kg/m²"),
        // vital-signs-tracking: unidad para temperatura corporal.
        new("celsius", "Grados Celsius", "°C"),
    ];

    private static readonly IReadOnlyList<MetricSeed> Metrics =
    [
        new("glucose_fasting", "Glucosa en ayunas", "metabolic", "mg_dl"),
        new("weight", "Peso", "body_comp", "kg"),
        new("height", "Talla", "body_comp", "cm"),
        new("systolic_bp", "Presión arterial sistólica", "vital", "mmhg"),
        new("diastolic_bp", "Presión arterial diastólica", "vital", "mmhg"),
        new("heart_rate", "Frecuencia cardíaca", "vital", "bpm"),
        new("bmi", "Índice de masa corporal", "body_comp", "kg_m2"),
        new("body_fat", "Porcentaje de grasa corporal", "body_comp", "pct"),
        new("hba1c", "Hemoglobina glicosilada", "metabolic", "pct"),
        // vital-signs-tracking: métricas nuevas para el payload de signos vitales.
        new("o2_saturation", "Saturación de oxígeno", "vital", "pct"),
        new("temperature_c", "Temperatura corporal", "vital", "celsius"),
    ];

    private static readonly IReadOnlyList<ReferenceRangeSeed> ReferenceRanges =
    [
        new("glucose_fasting", null, null, null, 70m, 99m, "mg_dl", 0),
        new("systolic_bp", null, null, null, 90m, 120m, "mmhg", 0),
        new("diastolic_bp", null, null, null, 60m, 80m, "mmhg", 0),
        new("bmi", null, null, null, 18.5m, 24.9m, "kg_m2", 0),
        new("heart_rate", null, null, null, 60m, 100m, "bpm", 0),
        new("hba1c", null, null, null, 4.0m, 5.6m, "pct", 0),
        // vital-signs-tracking: rango de referencia clínica para SpO2 (pendiente
        // de validación del comité; no altera las reglas de debilidad/seguridad).
        new("o2_saturation", null, null, null, 94m, 100m, "pct", 0),
    ];

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed del catálogo de mediciones clínicas cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el seed del catálogo de mediciones clínicas");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        await SeedUnitsAsync(ct);
        var unitIds = await GetUnitIdsAsync(ct);

        await SeedMetricsAsync(unitIds, ct);
        var metricIds = await GetMetricIdsAsync(ct);

        await SeedReferenceRangesAsync(unitIds, metricIds, ct);

        logger.LogInformation(
            "Catálogo de mediciones clínicas sembrado: {UnitCount} unidades, {MetricCount} métricas, {RangeCount} rangos definidos",
            unitIds.Count, metricIds.Count, ReferenceRanges.Count);
    }

    private async Task SeedUnitsAsync(CancellationToken ct)
    {
        foreach (var unit in Units)
        {
            var exists = await WithContext(
                db => db.UnitOfMeasures.AnyAsync(x => x.Code == unit.Code, ct), ct);

            if (exists)
                continue;

            await WithContext(async db =>
            {
                db.UnitOfMeasures.Add(new UnitOfMeasure
                {
                    Code = unit.Code,
                    Name = unit.Name,
                    Symbol = unit.Symbol,
                });
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            logger.LogInformation("Unidad sembrada: {Code} ({Symbol})", unit.Code, unit.Symbol);
        }
    }

    private async Task SeedMetricsAsync(Dictionary<string, Guid> unitIds, CancellationToken ct)
    {
        foreach (var metric in Metrics)
        {
            var exists = await WithContext(
                db => db.MeasurementMetrics.AnyAsync(x => x.Code == metric.Code, ct), ct);

            if (exists)
                continue;

            if (!unitIds.TryGetValue(metric.DefaultUnitCode, out var unitId))
            {
                logger.LogWarning(
                    "Métrica {Code} omitida: la unidad por defecto {UnitCode} no existe en el catálogo",
                    metric.Code, metric.DefaultUnitCode);
                continue;
            }

            await WithContext(async db =>
            {
                db.MeasurementMetrics.Add(new MeasurementMetric
                {
                    Code = metric.Code,
                    Name = metric.Name,
                    Category = metric.Category,
                    DefaultUnitId = unitId,
                });
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            logger.LogInformation("Métrica sembrada: {Code} ({Category})", metric.Code, metric.Category);
        }
    }

    private async Task SeedReferenceRangesAsync(
        Dictionary<string, Guid> unitIds,
        Dictionary<string, Guid> metricIds,
        CancellationToken ct)
    {
        var seeded = 0;

        foreach (var range in ReferenceRanges)
        {
            if (!metricIds.TryGetValue(range.MetricCode, out var metricId))
            {
                logger.LogWarning(
                    "Rango omitido: la métrica {MetricCode} no existe en el catálogo",
                    range.MetricCode);
                continue;
            }

            if (!unitIds.TryGetValue(range.UnitCode, out var unitId))
            {
                logger.LogWarning(
                    "Rango omitido: la unidad {UnitCode} no existe en el catálogo",
                    range.UnitCode);
                continue;
            }

            var exists = await WithContext(
                db => db.MeasurementReferenceRanges.AnyAsync(
                    x => x.MetricId == metricId
                        && x.MinValue == range.MinValue
                        && x.MaxValue == range.MaxValue
                        && x.UnitId == unitId, ct), ct);

            if (exists)
                continue;

            await WithContext(async db =>
            {
                db.MeasurementReferenceRanges.Add(new MeasurementReferenceRange
                {
                    MetricId = metricId,
                    AgeMin = range.AgeMin,
                    AgeMax = range.AgeMax,
                    Gender = range.Gender,
                    MinValue = range.MinValue,
                    MaxValue = range.MaxValue,
                    UnitId = unitId,
                    Priority = range.Priority,
                });
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            seeded++;
        }

        logger.LogInformation("Rangos de referencia sembrados: {Count}", seeded);
    }

    private async Task<Dictionary<string, Guid>> GetUnitIdsAsync(CancellationToken ct) =>
        await WithContext(
            db => db.UnitOfMeasures
                .Where(x => x.IsActive)
                .ToDictionaryAsync(x => x.Code, x => x.Id, ct), ct);

    private async Task<Dictionary<string, Guid>> GetMetricIdsAsync(CancellationToken ct) =>
        await WithContext(
            db => db.MeasurementMetrics
                .Where(x => x.IsActive)
                .ToDictionaryAsync(x => x.Code, x => x.Id, ct), ct);

    /// <summary>
    /// Ejecuta una operación con un scope propio: cada llamada resuelve un
    /// DbContext distinto, evitando conflictos de tracking EF entre operaciones.
    /// </summary>
    private async Task<T> WithContext<T>(Func<AppDbContext, Task<T>> action, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }
}