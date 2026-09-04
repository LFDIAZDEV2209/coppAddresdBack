using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Procesa los eventos de métricas de Tests de Salud en segundo plano sin bloquear
/// las evaluaciones clínicas ni la emisión de respuestas de pacientes.
/// </summary>
public sealed class HealthTestMetricsProcessorHostedService(
    IHealthTestMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<HealthTestMetricsProcessorHostedService> logger) : BackgroundService
{
    private static readonly Guid GlobalId = Guid.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador en segundo plano de métricas de Tests de Salud.");

        await foreach (var metricEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                switch (metricEvent)
                {
                    case HealthTestAssignedMetricEvent assignEvent:
                        await ProcessAssignedAsync(dbContext, assignEvent, stoppingToken);
                        break;

                    case HealthTestCompletedMetricEvent compEvent:
                        await ProcessCompletedAsync(dbContext, compEvent, stoppingToken);
                        break;

                    case HealthTestAlertTransitionedMetricEvent alertEvent:
                        await ProcessAlertTransitionAsync(dbContext, alertEvent, stoppingToken);
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error procesando evento de métrica de test de salud: {@Event}", metricEvent);
            }
        }
    }

    private static async Task ProcessAssignedAsync(
        AppDbContext dbContext,
        HealthTestAssignedMetricEvent e,
        CancellationToken ct)
    {
        var clinicId = e.ClinicId ?? GlobalId;

        const string sqlUpsert = """
            INSERT INTO app.health_test_daily_metrics (metric_date, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
            VALUES 
                (@p0, @p1, 'assignments_count', 'pending', 1, NOW()),
                (@p0, @p2, 'assignments_count', 'pending', 1, NOW())
            ON CONFLICT (metric_date, clinic_id, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = app.health_test_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlUpsert,
            [e.MetricDate, GlobalId, clinicId],
            ct);
    }

    private static async Task ProcessCompletedAsync(
        AppDbContext dbContext,
        HealthTestCompletedMetricEvent e,
        CancellationToken ct)
    {
        var clinicId = e.ClinicId ?? GlobalId;
        var severityDim = e.Severity?.ToString().ToLowerInvariant() ?? "none";
        var testCodeDim = string.IsNullOrWhiteSpace(e.TestCode) ? "general" : e.TestCode;

        // 1. Asignaciones completadas
        const string sqlComplete = """
            INSERT INTO app.health_test_daily_metrics (metric_date, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
            VALUES 
                (@p0, @p1, 'assignments_count', 'completed', 1, NOW()),
                (@p0, @p1, 'evaluations_count', 'completed', 1, NOW()),
                (@p0, @p1, 'test_completions', @p2, 1, NOW()),
                (@p0, @p3, 'assignments_count', 'completed', 1, NOW()),
                (@p0, @p3, 'evaluations_count', 'completed', 1, NOW()),
                (@p0, @p3, 'test_completions', @p2, 1, NOW())
            ON CONFLICT (metric_date, clinic_id, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = app.health_test_daily_metrics.total_count + 1,
                last_updated_at = NOW();

            UPDATE app.health_test_daily_metrics 
            SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
            WHERE metric_date = @p0 AND metric_key = 'assignments_count' AND (dimension_key = 'pending' OR dimension_key = 'in_progress')
              AND (clinic_id = @p1 OR clinic_id = @p3);
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlComplete,
            [e.MetricDate, GlobalId, testCodeDim, clinicId],
            ct);

        // 2. Severidad
        if (e.Severity.HasValue)
        {
            const string sqlSeverity = """
                INSERT INTO app.health_test_daily_metrics (metric_date, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
                VALUES 
                    (@p0, @p1, 'severity_count', @p2, 1, NOW()),
                    (@p0, @p3, 'severity_count', @p2, 1, NOW())
                ON CONFLICT (metric_date, clinic_id, metric_key, dimension_key)
                DO UPDATE SET 
                    total_count = app.health_test_daily_metrics.total_count + 1,
                    last_updated_at = NOW();
                """;

            await dbContext.Database.ExecuteSqlRawAsync(
                sqlSeverity,
                [e.MetricDate, GlobalId, severityDim, clinicId],
                ct);
        }

        // 3. Alertas creadas
        if (e.CreatedAlertsCount > 0)
        {
            const string sqlAlerts = """
                INSERT INTO app.health_test_daily_metrics (metric_date, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
                VALUES 
                    (@p0, @p1, 'alerts_count', 'active', @p2, NOW()),
                    (@p0, @p3, 'alerts_count', 'active', @p2, NOW())
                ON CONFLICT (metric_date, clinic_id, metric_key, dimension_key)
                DO UPDATE SET 
                    total_count = app.health_test_daily_metrics.total_count + @p2,
                    last_updated_at = NOW();
                """;

            await dbContext.Database.ExecuteSqlRawAsync(
                sqlAlerts,
                [e.MetricDate, GlobalId, e.CreatedAlertsCount, clinicId],
                ct);
        }
    }

    private static async Task ProcessAlertTransitionAsync(
        AppDbContext dbContext,
        HealthTestAlertTransitionedMetricEvent e,
        CancellationToken ct)
    {
        var clinicId = e.ClinicId ?? GlobalId;
        var oldDim = e.OldStatus.ToString().ToLowerInvariant();
        var newDim = e.NewStatus.ToString().ToLowerInvariant();

        const string sqlAlertTransition = """
            INSERT INTO app.health_test_daily_metrics (metric_date, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
            VALUES 
                (@p0, @p1, 'alerts_count', @p2, 1, NOW()),
                (@p0, @p3, 'alerts_count', @p2, 1, NOW())
            ON CONFLICT (metric_date, clinic_id, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = app.health_test_daily_metrics.total_count + 1,
                last_updated_at = NOW();

            UPDATE app.health_test_daily_metrics 
            SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
            WHERE metric_date = @p0 AND metric_key = 'alerts_count' AND dimension_key = @p4 
              AND (clinic_id = @p1 OR clinic_id = @p3);
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlAlertTransition,
            [e.MetricDate, GlobalId, newDim, clinicId, oldDim],
            ct);
    }
}
