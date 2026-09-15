using System.Diagnostics;
using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace CoppAddresd.Telemedicine.Infrastructure.Metrics;

/// <summary>
/// Reconstrucción set-based de las métricas pre-agregadas de analytics desde
/// <c>tele.appointments</c>. Replica exactamente la semántica del processor
/// incremental (<see cref="TelemedicineMetricsProcessorHostedService"/>):
/// día de agenda = fecha UTC de <c>scheduled_start</c>, hora UTC en
/// <c>Hour_HH</c>, dimensión de estado = nombre del enum y filas espejo
/// globales (<c>Guid.Empty</c>).
///
/// Semántica de escritura autoritativa (sobrescribe, no suma): el recálculo
/// gana sobre los incrementos del processor para los días tocados. Si llegan
/// eventos concurrentes durante la ejecución, re-ejecutar converge (el
/// segundo recálculo ya incluye esos eventos). Por eso se recomienda correrlo
/// en horario de bajo tráfico y repetirlo una vez para converger.
///
/// Transacción explícita corta vía <c>CreateExecutionStrategy</c> (obligatorio
/// con <c>EnableRetryOnFailure</c>). Con <c>dryRun</c> hace rollback y solo
/// reporta conteos.
/// </summary>
public sealed class MetricsBackfillService(
    TelemedicineDbContext dbContext,
    ILogger<MetricsBackfillService> logger
) : IMetricsBackfillService
{
    // Base común: una fila por cita en el rango, con las dimensiones ya
    // derivadas igual que en los eventos del processor. El filtro de clínica
    // es opcional (NULL = todas).
    private const string BaseCte = """
        WITH base AS (
            SELECT
                (a.scheduled_start AT TIME ZONE 'UTC')::date AS metric_date,
                a.professional_id AS professional_id,
                a.clinic_id AS clinic_id,
                a.patient_id AS patient_id,
                a.status::text AS status,
                'Hour_' || TO_CHAR(a.scheduled_start AT TIME ZONE 'UTC', 'HH24') AS hour_dim
            FROM tele.appointments AS a
            WHERE (a.scheduled_start AT TIME ZONE 'UTC')::date BETWEEN @p0 AND @p1
              AND (@p2 IS NULL OR a.clinic_id = @p2)
        )
        """;

    public async Task<BackfillMetricsResult> BackfillAsync(
        DateOnly from,
        DateOnly to,
        Guid? clinicId,
        bool dryRun,
        CancellationToken ct
    )
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "Iniciando backfill de métricas {From}..{To} (clinica: {Clinic}, dryRun: {DryRun}).",
            from,
            to,
            clinicId?.ToString() ?? "todas",
            dryRun
        );

        // Parámetro tipado para que el NULL de clínica resuelva a uuid en PG.
        NpgsqlParameter ClinicParam() =>
            new("@p2", NpgsqlDbType.Uuid) { Value = (object?)clinicId ?? DBNull.Value };

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<BackfillMetricsResult>(
            async operationCt =>
            {
                await using var tx = await dbContext.Database.BeginTransactionAsync(operationCt);
                try
                {
                    var considered = await dbContext
                        .Database.SqlQueryRaw<long>(
                            BaseCte + "SELECT COUNT(*) AS \"Value\" FROM base",
                            from,
                            to,
                            ClinicParam()
                        )
                        .SingleAsync(operationCt);

                    var metricsRows = await dbContext.Database.ExecuteSqlRawAsync(
                        UpsertMetricsSql(),
                        [from, to, ClinicParam()],
                        operationCt
                    );

                    var statsRows = await dbContext.Database.ExecuteSqlRawAsync(
                        UpsertStatsSql(),
                        [from, to, ClinicParam()],
                        operationCt
                    );

                    if (dryRun)
                    {
                        await tx.RollbackAsync(operationCt);
                    }
                    else
                    {
                        await tx.CommitAsync(operationCt);
                    }

                    sw.Stop();
                    logger.LogInformation(
                        "Backfill de métricas {From}..{To} terminado (dryRun: {DryRun}): {Appointments} citas, {Metrics} filas de métricas, {Stats} filas de stats en {Elapsed}.",
                        from,
                        to,
                        dryRun,
                        considered,
                        metricsRows,
                        statsRows,
                        sw.Elapsed
                    );

                    return new BackfillMetricsResult(
                        from,
                        to,
                        dryRun,
                        considered,
                        metricsRows,
                        statsRows,
                        sw.Elapsed,
                        dryRun
                            ? "Simulación: no se escribió nada."
                            : "Si hubo tráfico concurrente, re-ejecutar una vez para converger."
                    );
                }
                catch
                {
                    await tx.RollbackAsync(operationCt);
                    throw;
                }
            },
            ct
        );
    }

    /// <summary>
    /// Agrega a la granularidad exacta de la PK
    /// (día, profesional, clave, dimensión) las tres claves
    /// (<c>daily_total</c>, <c>status_count</c>, <c>hourly_count</c>) más el
    /// espejo global, y las sobrescribe (recálculo autoritativo).
    /// La clínica NO forma parte de la PK: se conserva solo como dato
    /// informativo (MAX). Agrupar por clínica generaría dos filas con la misma
    /// PK cuando un profesional atiende en dos clínicas el mismo día y el
    /// ON CONFLICT abortaría con 21000.
    /// </summary>
    private static string UpsertMetricsSql() =>
        BaseCte
        + $"""
            , agg AS (
                SELECT metric_date, professional_id,
                    MAX(clinic_id::text)::uuid AS clinic_id,
                    '{TelemedicineMetricKeys.DailyTotal}' AS metric_key,
                    '{TelemedicineMetricKeys.GeneralDimension}' AS dimension_key,
                    COUNT(*) AS total
                FROM base GROUP BY 1, 2
            UNION ALL
            SELECT metric_date, professional_id,
                MAX(clinic_id::text)::uuid AS clinic_id,
                '{TelemedicineMetricKeys.StatusCount}' AS metric_key,
                status AS dimension_key,
                COUNT(*) AS total
            FROM base GROUP BY 1, 2, 5
            UNION ALL
            SELECT metric_date, professional_id,
                MAX(clinic_id::text)::uuid AS clinic_id,
                '{TelemedicineMetricKeys.HourlyCount}' AS metric_key,
                hour_dim AS dimension_key,
                COUNT(*) AS total
            FROM base GROUP BY 1, 2, 5
            ),
            scoped AS (
                SELECT metric_date, professional_id, clinic_id, metric_key, dimension_key, total FROM agg
                UNION ALL
                SELECT metric_date, '{TelemedicineMetricKeys.GlobalProfessionalId}'::uuid,
                    MAX(clinic_id::text)::uuid, metric_key, dimension_key, SUM(total)
                FROM agg GROUP BY 1, 4, 5
            )
            INSERT INTO tele.appointment_daily_metrics
                (metric_date, professional_id, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
            SELECT metric_date, professional_id, clinic_id, metric_key, dimension_key, total, NOW() FROM scoped
            ON CONFLICT (metric_date, professional_id, metric_key, dimension_key)
            DO UPDATE SET
                total_count = EXCLUDED.total_count,
                clinic_id = EXCLUDED.clinic_id,
                last_updated_at = NOW()
            """;

    /// <summary>
    /// Agrega a la granularidad exacta de la PK (profesional, día) los
    /// contadores de stats con pacientes únicos, y los sobrescribe (recálculo
    /// autoritativo). La clínica no forma parte de la PK: solo informativa
    /// (MAX), igual que en <see cref="UpsertMetricsSql"/>.
    /// </summary>
    private static string UpsertStatsSql() =>
        BaseCte
        + $"""
            , agg AS (
                SELECT professional_id, metric_date,
                    MAX(clinic_id::text)::uuid AS clinic_id,
                    COUNT(*) AS total,
                    COUNT(*) FILTER (WHERE status = '{nameof(
                AppointmentStatus.Completed
            )}') AS completed,
                    COUNT(*) FILTER (WHERE status = '{nameof(
                AppointmentStatus.Cancelled
            )}') AS cancelled,
                    COUNT(*) FILTER (WHERE status = '{nameof(
                AppointmentStatus.NoShow
            )}') AS no_show,
                    COUNT(DISTINCT patient_id) AS unique_patients
                FROM base GROUP BY 1, 2
            )
            INSERT INTO tele.professional_daily_stats
                (professional_id, metric_date, clinic_id, total_appointments, completed_appointments,
                 cancelled_appointments, no_show_appointments, unique_patients, last_updated_at)
            SELECT professional_id, metric_date, clinic_id, total, completed, cancelled,
                no_show, unique_patients, NOW() FROM agg
            ON CONFLICT (professional_id, metric_date)
            DO UPDATE SET
                total_appointments = EXCLUDED.total_appointments,
                completed_appointments = EXCLUDED.completed_appointments,
                cancelled_appointments = EXCLUDED.cancelled_appointments,
                no_show_appointments = EXCLUDED.no_show_appointments,
                unique_patients = EXCLUDED.unique_patients,
                clinic_id = EXCLUDED.clinic_id,
                last_updated_at = NOW()
            """;
}
