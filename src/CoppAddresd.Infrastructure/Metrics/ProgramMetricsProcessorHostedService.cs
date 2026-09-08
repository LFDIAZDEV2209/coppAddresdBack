using CoppAddresd.Application.Features.ProgramProgress.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Procesa los eventos de métricas de forma asíncrona en segundo plano, ejecutando
/// operaciones atómicas de Upsert en PostgreSQL sin bloquear a los usuarios activos.
/// </summary>
public sealed class ProgramMetricsProcessorHostedService(
    IProgramMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ProgramMetricsProcessorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador en segundo plano de métricas del programa ANTARES.");

        await foreach (var metricEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                switch (metricEvent)
                {
                    case TaskCompletedMetricEvent taskEvent:
                        await ProcessTaskCompletedAsync(dbContext, taskEvent, stoppingToken);
                        break;

                    case XpAwardedMetricEvent xpEvent:
                        await ProcessXpAwardedAsync(dbContext, xpEvent, stoppingToken);
                        break;

                    case StreakUpdatedMetricEvent streakEvent:
                        await ProcessStreakUpdatedAsync(dbContext, streakEvent, stoppingToken);
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error procesando evento de métrica en segundo plano: {@Event}", metricEvent);
            }
        }
    }

    private static async Task ProcessTaskCompletedAsync(
        AppDbContext dbContext,
        TaskCompletedMetricEvent e,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO app.program_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
            VALUES 
                (@p0, 'tasks_completed_today', 'general', 1, 0, NOW()),
                (@p0, 'tasks_completed_by_code', @p1, 1, 0, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = app.program_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, [e.LocalDate, e.TaskCode], ct);
    }

    private static async Task ProcessXpAwardedAsync(
        AppDbContext dbContext,
        XpAwardedMetricEvent e,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO app.program_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
            VALUES 
                (@p0, 'xp_total_daily', 'general', 1, @p2, NOW()),
                (@p0, 'xp_awarded_by_reason', @p1, 1, @p2, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = app.program_daily_metrics.total_count + 1,
                total_value = app.program_daily_metrics.total_value + @p2,
                last_updated_at = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, [e.AwardDate, e.Reason, (decimal)e.Amount], ct);
    }

    private static async Task ProcessStreakUpdatedAsync(
        AppDbContext dbContext,
        StreakUpdatedMetricEvent e,
        CancellationToken ct)
    {
        var bucket = GetStreakBucket(e.CurrentStreak);
        const string sql = """
            INSERT INTO app.program_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
            VALUES (@p0, 'streak_distribution', @p1, 1, 0, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = app.program_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, [e.LocalDate, bucket], ct);
    }

    private static string GetStreakBucket(int streak) => streak switch
    {
        0 => "0_days",
        >= 1 and <= 3 => "1-3_days",
        >= 4 and <= 7 => "4-7_days",
        >= 8 and <= 14 => "8-14_days",
        >= 15 and <= 30 => "15-30_days",
        _ => "30+_days"
    };
}
