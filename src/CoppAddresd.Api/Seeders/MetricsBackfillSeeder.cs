using System.Diagnostics;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Reconciliación y Backfill histórico de métricas CQRS (Channel Pattern).
/// Recorre los registros históricos existentes en las tablas transaccionales (OLTP)
/// y puebla de forma idempotente (INSERT ... ON CONFLICT DO UPDATE) los rollups
/// diarios de métricas de la plataforma al arrancar la API.
/// </summary>
public sealed class MetricsBackfillSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<MetricsBackfillSeeder> logger) : IHostedService
{
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunBackfillAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Backfill de métricas CQRS cancelado.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante la ejecución del Backfill de métricas CQRS.");
        }
    }

    /// <summary>
    /// Ejecuta la reconciliación completa de métricas de todas las tablas de la base de datos.
    /// </summary>
    public async Task<int> RunBackfillAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Iniciando Backfill y reconciliación de tablas de métricas CQRS...");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var totalOperations = 0;

        // 1. Métricas de Pacientes (app.patient_daily_metrics)
        // Nota: Las columnas fueron generadas con comillas PascalCase ("MetricDate", "ClinicId", etc.)
        try
        {
            const string sqlPatients = """
                -- Total pacientes histórico (global)
                INSERT INTO app.patient_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
                SELECT 
                    created_at::date AS "MetricDate",
                    '00000000-0000-0000-0000-000000000000'::uuid AS "ClinicId",
                    'total_patients' AS "MetricKey",
                    'general' AS "DimensionKey",
                    COUNT(*)::bigint AS "TotalCount",
                    NOW() AS "LastUpdatedAt"
                FROM app.patient_profiles
                WHERE deleted_at IS NULL
                GROUP BY created_at::date
                ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
                DO UPDATE SET 
                    "TotalCount" = EXCLUDED."TotalCount",
                    "LastUpdatedAt" = NOW();

                -- Nuevos pacientes por fecha (global)
                INSERT INTO app.patient_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
                SELECT 
                    created_at::date AS "MetricDate",
                    '00000000-0000-0000-0000-000000000000'::uuid AS "ClinicId",
                    'new_patients' AS "MetricKey",
                    'general' AS "DimensionKey",
                    COUNT(*)::bigint AS "TotalCount",
                    NOW() AS "LastUpdatedAt"
                FROM app.patient_profiles
                WHERE deleted_at IS NULL
                GROUP BY created_at::date
                ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
                DO UPDATE SET 
                    "TotalCount" = EXCLUDED."TotalCount",
                    "LastUpdatedAt" = NOW();

                -- Distribución por estado
                INSERT INTO app.patient_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
                SELECT 
                    created_at::date AS "MetricDate",
                    '00000000-0000-0000-0000-000000000000'::uuid AS "ClinicId",
                    'status_count' AS "MetricKey",
                    status AS "DimensionKey",
                    COUNT(*)::bigint AS "TotalCount",
                    NOW() AS "LastUpdatedAt"
                FROM app.patient_profiles
                WHERE deleted_at IS NULL
                GROUP BY created_at::date, status
                ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
                DO UPDATE SET 
                    "TotalCount" = EXCLUDED."TotalCount",
                    "LastUpdatedAt" = NOW();

                -- Distribución por género
                INSERT INTO app.patient_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
                SELECT 
                    created_at::date AS "MetricDate",
                    '00000000-0000-0000-0000-000000000000'::uuid AS "ClinicId",
                    'gender_distribution' AS "MetricKey",
                    COALESCE(NULLIF(TRIM(gender), ''), 'Desconocido') AS "DimensionKey",
                    COUNT(*)::bigint AS "TotalCount",
                    NOW() AS "LastUpdatedAt"
                FROM app.patient_profiles
                WHERE deleted_at IS NULL
                GROUP BY created_at::date, COALESCE(NULLIF(TRIM(gender), ''), 'Desconocido')
                ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
                DO UPDATE SET 
                    "TotalCount" = EXCLUDED."TotalCount",
                    "LastUpdatedAt" = NOW();
                """;
            totalOperations += await db.Database.ExecuteSqlRawAsync(sqlPatients, ct);
            logger.LogInformation("Reconciliación de métricas de pacientes completada.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aviso reconciliando app.patient_daily_metrics.");
        }

        // 2. Métricas de Tests de Salud (app.health_test_daily_metrics)
        try
        {
            const string sqlHealthTests = """
                INSERT INTO app.health_test_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
                SELECT 
                    COALESCE(completed_at, assigned_at)::date AS "MetricDate",
                    '00000000-0000-0000-0000-000000000000'::uuid AS "ClinicId",
                    'assignments_count' AS "MetricKey",
                    LOWER(status) AS "DimensionKey",
                    COUNT(*)::bigint AS "TotalCount",
                    NOW() AS "LastUpdatedAt"
                FROM app.health_test_assignments
                GROUP BY COALESCE(completed_at, assigned_at)::date, LOWER(status)
                ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
                DO UPDATE SET 
                    "TotalCount" = EXCLUDED."TotalCount",
                    "LastUpdatedAt" = NOW();

                INSERT INTO app.health_test_daily_metrics ("MetricDate", "ClinicId", "MetricKey", "DimensionKey", "TotalCount", "LastUpdatedAt")
                SELECT 
                    completed_at::date AS "MetricDate",
                    '00000000-0000-0000-0000-000000000000'::uuid AS "ClinicId",
                    'evaluations_count' AS "MetricKey",
                    'completed' AS "DimensionKey",
                    COUNT(*)::bigint AS "TotalCount",
                    NOW() AS "LastUpdatedAt"
                FROM app.health_test_assignments
                WHERE completed_at IS NOT NULL
                GROUP BY completed_at::date
                ON CONFLICT ("MetricDate", "ClinicId", "MetricKey", "DimensionKey")
                DO UPDATE SET 
                    "TotalCount" = EXCLUDED."TotalCount",
                    "LastUpdatedAt" = NOW();
                """;
            totalOperations += await db.Database.ExecuteSqlRawAsync(sqlHealthTests, ct);
            logger.LogInformation("Reconciliación de métricas de tests de salud completada.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aviso reconciliando app.health_test_daily_metrics.");
        }

        // 3. Métricas de Inventario & Farmacia (erp.inventory_daily_metrics)
        try
        {
            const string sqlInventory = """
                -- Entradas de inventario (columna 'date' en erp.inventory_entries)
                INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
                SELECT 
                    COALESCE(date, created_at::date) AS metric_date,
                    'entries_count' AS metric_key,
                    'total' AS dimension_key,
                    COUNT(*)::bigint AS total_count,
                    NOW() AS last_updated_at
                FROM erp.inventory_entries
                GROUP BY COALESCE(date, created_at::date)
                ON CONFLICT (metric_date, metric_key, dimension_key)
                DO UPDATE SET 
                    total_count = EXCLUDED.total_count,
                    last_updated_at = NOW();

                -- Salidas de inventario (columna 'date' en erp.inventory_exits)
                INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
                SELECT 
                    COALESCE(date, created_at::date) AS metric_date,
                    'exits_count' AS metric_key,
                    'total' AS dimension_key,
                    COUNT(*)::bigint AS total_count,
                    NOW() AS last_updated_at
                FROM erp.inventory_exits
                GROUP BY COALESCE(date, created_at::date)
                ON CONFLICT (metric_date, metric_key, dimension_key)
                DO UPDATE SET 
                    total_count = EXCLUDED.total_count,
                    last_updated_at = NOW();
                """;
            totalOperations += await db.Database.ExecuteSqlRawAsync(sqlInventory, ct);
            logger.LogInformation("Reconciliación de métricas de inventario completada.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aviso reconciliando erp.inventory_daily_metrics.");
        }

        // 4. Métricas de Programa ANTARES (app.program_daily_metrics)
        try
        {
            const string sqlProgram = """
                INSERT INTO app.program_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
                SELECT 
                    completed_at::date AS metric_date,
                    'tasks_completed_today' AS metric_key,
                    'general' AS dimension_key,
                    COUNT(*)::bigint AS total_count,
                    0::numeric AS total_value,
                    NOW() AS last_updated_at
                FROM app.task_completions
                WHERE completed_at IS NOT NULL
                GROUP BY completed_at::date
                ON CONFLICT (metric_date, metric_key, dimension_key)
                DO UPDATE SET 
                    total_count = EXCLUDED.total_count,
                    last_updated_at = NOW();
                """;
            totalOperations += await db.Database.ExecuteSqlRawAsync(sqlProgram, ct);
            logger.LogInformation("Reconciliación de métricas de programa completada.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aviso reconciliando app.program_daily_metrics.");
        }

        // 5. Métricas de Telemedicina y Citas (tele.appointment_daily_metrics) si el schema existe
        try
        {
            const string sqlTelemedicine = """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'tele' AND table_name = 'appointments')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'tele' AND table_name = 'appointment_daily_metrics') THEN
                        INSERT INTO tele.appointment_daily_metrics (metric_date, professional_id, metric_key, dimension_key, total_count, last_updated_at)
                        SELECT 
                            scheduled_start::date AS metric_date,
                            COALESCE(professional_id, '00000000-0000-0000-0000-000000000000'::uuid) AS professional_id,
                            'appointments_count' AS metric_key,
                            COALESCE(LOWER(status), 'general') AS dimension_key,
                            COUNT(*)::bigint AS total_count,
                            NOW() AS last_updated_at
                        FROM tele.appointments
                        GROUP BY scheduled_start::date, COALESCE(professional_id, '00000000-0000-0000-0000-000000000000'::uuid), COALESCE(LOWER(status), 'general')
                        ON CONFLICT (metric_date, professional_id, metric_key, dimension_key)
                        DO UPDATE SET 
                            total_count = EXCLUDED.total_count,
                            last_updated_at = NOW();
                    END IF;
                END $$;
                """;
            await db.Database.ExecuteSqlRawAsync(sqlTelemedicine, ct);
            logger.LogInformation("Reconciliación de métricas de telemedicina verificada/completada.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aviso reconciliando tele.appointment_daily_metrics.");
        }

        sw.Stop();
        logger.LogInformation(
            "Backfill y reconciliación de todas las tablas de métricas finalizada con éxito en {ElapsedMs} ms.",
            sw.ElapsedMilliseconds);

        return totalOperations;
    }
}
