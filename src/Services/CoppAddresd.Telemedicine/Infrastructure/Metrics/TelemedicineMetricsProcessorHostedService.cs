using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Infrastructure.Metrics;

/// <summary>
/// Procesa los eventos de métricas de telemedicina en segundo plano sin bloquear
/// las transacciones críticas de agendamiento y salas virtuales.
/// </summary>
public sealed class TelemedicineMetricsProcessorHostedService(
    ITelemedicineMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<TelemedicineMetricsProcessorHostedService> logger) : BackgroundService
{
    private static readonly Guid GlobalId = Guid.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador en segundo plano de métricas de Telemedicina.");

        await foreach (var metricEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TelemedicineDbContext>();

                switch (metricEvent)
                {
                    case AppointmentScheduledMetricEvent scheduledEvent:
                        await ProcessScheduledAsync(dbContext, scheduledEvent, stoppingToken);
                        break;

                    case AppointmentStatusChangedMetricEvent statusEvent:
                        await ProcessStatusChangedAsync(dbContext, statusEvent, stoppingToken);
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error procesando evento de métrica de telemedicina: {@Event}", metricEvent);
            }
        }
    }

    private static async Task ProcessScheduledAsync(
        TelemedicineDbContext dbContext,
        AppointmentScheduledMetricEvent e,
        CancellationToken ct)
    {
        var hourDim = $"Hour_{e.ScheduledHour:D2}";
        var statusDim = e.Status.ToString();

        // 1. Upsert en appointment_daily_metrics (tanto Global como por Profesional)
        const string sqlMetrics = """
            INSERT INTO tele.appointment_daily_metrics (metric_date, professional_id, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
            VALUES 
                (@p0, @p1, @p2, 'daily_total', 'general', 1, NOW()),
                (@p0, @p1, @p2, 'status_count', @p3, 1, NOW()),
                (@p0, @p1, @p2, 'hourly_count', @p4, 1, NOW()),
                (@p0, @p5, @p2, 'daily_total', 'general', 1, NOW()),
                (@p0, @p5, @p2, 'status_count', @p3, 1, NOW()),
                (@p0, @p5, @p2, 'hourly_count', @p4, 1, NOW())
            ON CONFLICT (metric_date, professional_id, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = tele.appointment_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlMetrics,
            [e.ScheduledDate, GlobalId, (object?)e.ClinicId ?? DBNull.Value, statusDim, hourDim, e.ProfessionalId],
            ct);

        // 2. Upsert en professional_daily_stats
        const string sqlProf = """
            INSERT INTO tele.professional_daily_stats (professional_id, metric_date, clinic_id, total_appointments, completed_appointments, cancelled_appointments, no_show_appointments, unique_patients, last_updated_at)
            VALUES (@p0, @p1, @p2, 1, 0, 0, 0, 1, NOW())
            ON CONFLICT (professional_id, metric_date)
            DO UPDATE SET 
                total_appointments = tele.professional_daily_stats.total_appointments + 1,
                last_updated_at = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlProf,
            [e.ProfessionalId, e.ScheduledDate, (object?)e.ClinicId ?? DBNull.Value],
            ct);
    }

    private static async Task ProcessStatusChangedAsync(
        TelemedicineDbContext dbContext,
        AppointmentStatusChangedMetricEvent e,
        CancellationToken ct)
    {
        var oldDim = e.OldStatus.ToString();
        var newDim = e.NewStatus.ToString();

        // 1. Decrementar oldStatus e incrementar newStatus
        const string sqlStatus = """
            INSERT INTO tele.appointment_daily_metrics (metric_date, professional_id, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
            VALUES 
                (@p0, @p1, @p2, 'status_count', @p3, 1, NOW()),
                (@p0, @p4, @p2, 'status_count', @p3, 1, NOW())
            ON CONFLICT (metric_date, professional_id, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = tele.appointment_daily_metrics.total_count + 1,
                last_updated_at = NOW();

            UPDATE tele.appointment_daily_metrics 
            SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
            WHERE metric_date = @p0 AND metric_key = 'status_count' AND dimension_key = @p5 
              AND (professional_id = @p1 OR professional_id = @p4);
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlStatus,
            [e.ScheduledDate, GlobalId, (object?)e.ClinicId ?? DBNull.Value, newDim, e.ProfessionalId, oldDim],
            ct);

        // 2. Si es Completed, Cancelled o NoShow, actualizar tabla de stats del profesional
        var completedInc = e.NewStatus == AppointmentStatus.Completed ? 1 : 0;
        var cancelledInc = e.NewStatus == AppointmentStatus.Cancelled ? 1 : 0;
        var noShowInc = e.NewStatus == AppointmentStatus.NoShow ? 1 : 0;

        if (completedInc > 0 || cancelledInc > 0 || noShowInc > 0)
        {
            const string sqlProf = """
                INSERT INTO tele.professional_daily_stats (professional_id, metric_date, clinic_id, total_appointments, completed_appointments, cancelled_appointments, no_show_appointments, unique_patients, last_updated_at)
                VALUES (@p0, @p1, @p2, 1, @p3, @p4, @p5, 1, NOW())
                ON CONFLICT (professional_id, metric_date)
                DO UPDATE SET 
                    completed_appointments = tele.professional_daily_stats.completed_appointments + @p3,
                    cancelled_appointments = tele.professional_daily_stats.cancelled_appointments + @p4,
                    no_show_appointments = tele.professional_daily_stats.no_show_appointments + @p5,
                    last_updated_at = NOW();
                """;

            await dbContext.Database.ExecuteSqlRawAsync(
                sqlProf,
                [e.ProfessionalId, e.ScheduledDate, (object?)e.ClinicId ?? DBNull.Value, completedInc, cancelledInc, noShowInc],
                ct);
        }
    }
}
