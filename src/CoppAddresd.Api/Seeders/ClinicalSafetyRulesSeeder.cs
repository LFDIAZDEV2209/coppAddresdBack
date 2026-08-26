using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seed de reglas de seguridad clínica para la generación de planes con IA
/// (referencia ADA/OMS conservadora, a validar clínicamente). Cada regla
/// condiciona el plan según el valor consolidado de una métrica del paciente.
///
/// Idempotente por (metric_code, operator, threshold_min, threshold_max,
/// unit_code). Cada operación usa un scope propio con su propio DbContext para
/// evitar conflictos de tracking EF, igual que
/// <see cref="ClinicalMeasurementsSeeder"/>.
/// </summary>
public sealed class ClinicalSafetyRulesSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<ClinicalSafetyRulesSeeder> logger) : IHostedService
{
    private sealed record RuleSeed(
        string MetricCode,
        string Operator,
        decimal? ThresholdMin,
        decimal? ThresholdMax,
        string UnitCode,
        string Restriction,
        SafetySeverity Severity,
        int SortOrder);

    private static readonly IReadOnlyList<RuleSeed> Rules =
    [
        new("glucose_fasting", ">", 126m, null, "mg_dl",
            "Evitar azúcares añadidos y carbohidratos de alto índice glucémico",
            SafetySeverity.Block, 10),
        new("bmi", ">", 30m, null, "kg_m2",
            "Ejercicio de bajo impacto: evitar saltos, correr y ejercicios de alto impacto articular",
            SafetySeverity.Block, 20),
        new("systolic_bp", ">", 140m, null, "mmhg",
            "Evitar ejercicio de alta intensidad; priorizar actividad moderada",
            SafetySeverity.Warning, 30),
        new("heart_rate", ">", 100m, null, "bpm",
            "Consultar antes de prescribir ejercicio intenso",
            SafetySeverity.Warning, 40),
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
            logger.LogInformation("Seed de reglas de seguridad clínica cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el seed de reglas de seguridad clínica");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        // Las reglas referencian métricas del catálogo por código: si una
        // métrica no existe, se omite la regla con warning (no es bloqueante).
        var metricCodes = await WithContext(
            db => db.MeasurementMetrics
                .Where(x => x.IsActive)
                .Select(x => x.Code)
                .ToHashSetAsync(ct), ct);

        var seeded = 0;

        foreach (var rule in Rules)
        {
            if (!metricCodes.Contains(rule.MetricCode))
            {
                logger.LogWarning(
                    "Regla {MetricCode} omitida: la métrica no existe en el catálogo",
                    rule.MetricCode);
                continue;
            }

            var exists = await WithContext(
                db => db.PlanSafetyRules.AnyAsync(
                    x => x.MetricCode == rule.MetricCode
                        && x.Operator == rule.Operator
                        && x.ThresholdMin == rule.ThresholdMin
                        && x.ThresholdMax == rule.ThresholdMax
                        && x.UnitCode == rule.UnitCode, ct), ct);

            if (exists)
                continue;

            await WithContext(async db =>
            {
                db.PlanSafetyRules.Add(new PlanSafetyRule
                {
                    MetricCode = rule.MetricCode,
                    Operator = rule.Operator,
                    ThresholdMin = rule.ThresholdMin,
                    ThresholdMax = rule.ThresholdMax,
                    UnitCode = rule.UnitCode,
                    Restriction = rule.Restriction,
                    Severity = rule.Severity,
                    SortOrder = rule.SortOrder,
                });
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            seeded++;
            logger.LogInformation(
                "Regla de seguridad sembrada: {MetricCode} {Operator} {ThresholdMin} {UnitCode} ({Severity})",
                rule.MetricCode, rule.Operator, rule.ThresholdMin, rule.UnitCode, rule.Severity);
        }

        logger.LogInformation("Reglas de seguridad clínicas sembradas: {Count}", seeded);
    }

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