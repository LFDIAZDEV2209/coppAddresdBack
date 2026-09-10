using CoppAddresd.Application.Features.Patients.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Procesa los eventos de métricas de pacientes en segundo plano sin bloquear
/// las transacciones de creación, actualización o asignación de pacientes.
/// </summary>
public sealed class PatientMetricsProcessorHostedService(
    IPatientMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PatientMetricsProcessorHostedService> logger) : BackgroundService
{
    private static readonly Guid GlobalId = Guid.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador en segundo plano de métricas de Pacientes y ERP.");

        await foreach (var metricEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                switch (metricEvent)
                {
                    case PatientRegisteredMetricEvent regEvent:
                        await ProcessRegisteredAsync(dbContext, regEvent, stoppingToken);
                        break;

                    case PatientStatusChangedMetricEvent statusEvent:
                        await ProcessStatusChangedAsync(dbContext, statusEvent, stoppingToken);
                        break;

                    case PatientAssignmentMetricEvent assignEvent:
                        await ProcessAssignmentAsync(dbContext, assignEvent, stoppingToken);
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error procesando evento de métrica de paciente: {@Event}", metricEvent);
            }
        }
    }

    private static async Task ProcessRegisteredAsync(
        AppDbContext dbContext,
        PatientRegisteredMetricEvent e,
        CancellationToken ct)
    {
        var metricDate = DateOnly.FromDateTime(e.CreatedAtUtc);
        var clinicId = e.ClinicId ?? GlobalId;
        var genderDim = string.IsNullOrWhiteSpace(e.Gender) ? "Desconocido" : e.Gender;
        var ageDim = ResolveAgeGroup(e.DateOfBirth, e.CreatedAtUtc);
        var insurerDim = e.InsurerId.HasValue ? e.InsurerId.Value.ToString() : "SinAseguradora";
        var statusDim = string.IsNullOrWhiteSpace(e.Status) ? "Activo" : e.Status;

        // Upsert atómico para métricas globales (GlobalId) y por clínica
        const string sqlUpsert = """
            INSERT INTO app.patient_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
            VALUES 
                (@p0, @p1, 'total_patients', 'general', 1, NOW()),
                (@p0, @p1, 'new_patients', 'general', 1, NOW()),
                (@p0, @p1, 'status_count', @p2, 1, NOW()),
                (@p0, @p1, 'gender_distribution', @p3, 1, NOW()),
                (@p0, @p1, 'age_group', @p4, 1, NOW()),
                (@p0, @p1, 'insurer_distribution', @p5, 1, NOW()),
                (@p0, @p1, 'unassigned_patients', 'general', 1, NOW()),
                (@p0, @p6, 'total_patients', 'general', 1, NOW()),
                (@p0, @p6, 'new_patients', 'general', 1, NOW()),
                (@p0, @p6, 'status_count', @p2, 1, NOW()),
                (@p0, @p6, 'gender_distribution', @p3, 1, NOW()),
                (@p0, @p6, 'age_group', @p4, 1, NOW()),
                (@p0, @p6, 'insurer_distribution', @p5, 1, NOW()),
                (@p0, @p6, 'unassigned_patients', 'general', 1, NOW())
            ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
            DO UPDATE SET 
                "TotalCount" = app.patient_daily_metrics."TotalCount" + 1,
                "LastUpdatedAt" = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlUpsert,
            [metricDate, GlobalId, statusDim, genderDim, ageDim, insurerDim, clinicId],
            ct);
    }

    private static async Task ProcessStatusChangedAsync(
        AppDbContext dbContext,
        PatientStatusChangedMetricEvent e,
        CancellationToken ct)
    {
        var metricDate = DateOnly.FromDateTime(e.ChangedAtUtc);
        var clinicId = e.ClinicId ?? GlobalId;

        const string sqlStatus = """
            INSERT INTO app.patient_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
            VALUES 
                (@p0, @p1, 'status_count', @p2, 1, NOW()),
                (@p0, @p3, 'status_count', @p2, 1, NOW())
            ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
            DO UPDATE SET 
                "TotalCount" = app.patient_daily_metrics."TotalCount" + 1,
                "LastUpdatedAt" = NOW();

            UPDATE app.patient_daily_metrics 
            SET "TotalCount" = GREATEST(0, "TotalCount" - 1), "LastUpdatedAt" = NOW()
            WHERE "MetricDate" = @p0 AND "MetricKey" = 'status_count' AND "DimensionKey" = @p4 
              AND ("ClinicId" = @p1 OR "ClinicId" = @p3);
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sqlStatus,
            [metricDate, GlobalId, e.NewStatus, clinicId, e.OldStatus],
            ct);
    }

    private static async Task ProcessAssignmentAsync(
        AppDbContext dbContext,
        PatientAssignmentMetricEvent e,
        CancellationToken ct)
    {
        var metricDate = DateOnly.FromDateTime(e.ChangedAtUtc);
        var clinicId = e.ClinicId ?? GlobalId;

        if (e.IsAssigned)
        {
            // Paciente asignado → decrementar sin profesional asignado
            const string sqlDecr = """
                UPDATE app.patient_daily_metrics 
                SET "TotalCount" = GREATEST(0, "TotalCount" - 1), "LastUpdatedAt" = NOW()
                WHERE "MetricDate" = @p0 AND "MetricKey" = 'unassigned_patients' AND "DimensionKey" = 'general'
                  AND ("ClinicId" = @p1 OR "ClinicId" = @p2);
                """;

            await dbContext.Database.ExecuteSqlRawAsync(
                sqlDecr,
                [metricDate, GlobalId, clinicId],
                ct);
        }
        else
        {
            // Paciente desasignado → incrementar sin profesional asignado
            const string sqlIncr = """
                INSERT INTO app.patient_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
                VALUES 
                    (@p0, @p1, 'unassigned_patients', 'general', 1, NOW()),
                    (@p0, @p2, 'unassigned_patients', 'general', 1, NOW())
                ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
                DO UPDATE SET 
                    "TotalCount" = app.patient_daily_metrics."TotalCount" + 1,
                    "LastUpdatedAt" = NOW();
                """;

            await dbContext.Database.ExecuteSqlRawAsync(
                sqlIncr,
                [metricDate, GlobalId, clinicId],
                ct);
        }
    }

    private static string ResolveAgeGroup(DateTime? dob, DateTime now)
    {
        if (!dob.HasValue) return "Desconocido";
        var age = now.Year - dob.Value.Year;
        if (dob.Value.Date > now.AddYears(-age)) age--;

        return age switch
        {
            < 18 => "0-17",
            <= 35 => "18-35",
            <= 50 => "36-50",
            <= 65 => "51-65",
            _ => "65+"
        };
    }
}
