using System.Threading;
using System.Threading.Tasks;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// SQL de recomputo (idempotente, set-based) del rollup diario de Biometría
/// (<c>app.biometria_daily_metrics</c>) desde las tablas OLTP
/// (<c>app.clinical_measurements</c> + <c>app.measurement_metrics</c>).
/// Lo usan el seed demo (<c>BiometriaSeeder</c>), el backfill de arranque
/// (<c>MetricsBackfillSeeder</c>) y el endpoint
/// <c>POST /api/v1/dashboard/maintenance/reconcile-metrics</c>.
///
/// Semántica espejo del <c>BiometriaMetricsProcessorHostedService</c> (CQRS
/// Channel Pattern): mismas categorías OMS/ADA del processor y, para grasa
/// corporal, los cortes del dashboard comunitario
/// (<c>ProgramRepository.ClassifyGrasa</c>, categorías
/// Óptimo/Normal/Alto/Obesidad) en lugar de la escala atlética; los valores
/// ignorados por el processor (&lt;= 0) también se ignoran aquí. La diferencia
/// deliberada es la operación: el processor ACUMULA
/// (<c>total_count = total_count + EXCLUDED.total_count</c>), el backfill
/// RECOMPUTA (<c>total_count = EXCLUDED.total_count</c>), de ahí que sea
/// idempotente (correrlo N veces deja el mismo estado). La única excepción es
/// <c>grasa_distribution</c>: antes de recomputarla se PURGA por completo
/// (<c>DELETE ... WHERE metric_key = 'grasa_distribution'</c>) para eliminar
/// categorías huérfanas del naming anterior (p. ej. <c>male_Esencial</c>), que
/// el upsert nunca tocaría. Las demás claves mantienen overwrite-upsert puro y
/// las claves solo de evento quedan intactas.
///
/// Fecha: <c>DATE(cm.observed_at)</c> (día UTC de la observación), la misma
/// derivación del backfill demo. El processor en vivo usa la fecha LOCAL del
/// paciente (<c>CompleteTaskCommandHandler</c> → <c>request.LocalDate</c>),
/// pero la medición solo guarda <c>observed_at = MeasuredAt ?? now()</c> (UTC)
/// y no la fecha local (reconstruirla exigiría la zona del enrollment), por lo
/// que en el borde de medianoche ambos días pueden diferir en ±1.
///
/// Claves SOLO de evento (el processor las emite y NO son reconstruibles desde
/// las mediciones): <c>glucosa_distribution</c> dimensión <c>"Sin dato"</c>
/// (tarea vitals completada sin glucosa) y <c>city_patient_count</c> (heat map
/// por ciudad del paciente). Este recomputo no las toca: solo hace upsert de
/// las filas derivables y deja intactas las filas de evento.
/// </summary>
public static class BiometriaRollupSql
{
    public const string RecomputeSql = """
        -- 1. IMC: distribución OMS por día (espejo del processor).
        INSERT INTO app.biometria_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
        SELECT
            DATE(cm.observed_at) AS metric_date,
            'imc_distribution' AS metric_key,
            CASE
                WHEN cm.value < 18.5 THEN 'Bajo peso'
                WHEN cm.value < 25   THEN 'Normal'
                WHEN cm.value < 30   THEN 'Sobrepeso'
                WHEN cm.value < 35   THEN 'Obesidad I'
                ELSE 'Obesidad II-III'
            END AS dimension_key,
            COUNT(*) AS total_count,
            SUM(cm.value) AS total_value,
            NOW() AS last_updated_at
        FROM app.clinical_measurements cm
        JOIN app.measurement_metrics mm ON cm.metric_id = mm.id
        WHERE mm.code = 'bmi'
          AND cm.value > 0
        GROUP BY DATE(cm.observed_at), CASE
            WHEN cm.value < 18.5 THEN 'Bajo peso'
            WHEN cm.value < 25   THEN 'Normal'
            WHEN cm.value < 30   THEN 'Sobrepeso'
            WHEN cm.value < 35   THEN 'Obesidad I'
            ELSE 'Obesidad II-III'
        END
        ON CONFLICT (metric_date, metric_key, dimension_key)
        DO UPDATE SET total_count = EXCLUDED.total_count, total_value = EXCLUDED.total_value, last_updated_at = NOW();

        -- 2. Grasa corporal: distribución por sexo (cortes del dashboard
        -- comunitario, ProgramRepository.ClassifyGrasa).
        -- Purga autoritativa de la clave completa antes de recomputar: los
        -- nombres de categoría cambiaron (escala atlética → Óptimo/Normal/
        -- Alto/Obesidad) y las filas viejas (p. ej. male_Esencial) quedarían
        -- huérfanas para siempre, porque el upsert solo toca las dimensiones
        -- que el recomputo vuelve a emitir.
        DELETE FROM app.biometria_daily_metrics WHERE metric_key = 'grasa_distribution';

        -- El sexo sale del perfil del paciente: masculino/m/male (trim,
        -- case-insensitive) → male; cualquier otro valor o null → female.
        WITH src AS (
            SELECT
                DATE(cm.observed_at) AS metric_date,
                cm.value AS value,
                CASE
                    WHEN LOWER(BTRIM(p.gender)) IN ('masculino', 'm', 'male') THEN 'male'
                    ELSE 'female'
                END AS sex_prefix
            FROM app.clinical_measurements cm
            JOIN app.measurement_metrics mm ON cm.metric_id = mm.id
            JOIN app.patient_profiles p ON p.id = cm.patient_id
            WHERE mm.code = 'body_fat'
              AND cm.value > 0
        ),
        base AS (
            SELECT
                metric_date,
                sex_prefix,
                CASE
                    WHEN sex_prefix = 'male' THEN
                        CASE
                            WHEN value <= 18 THEN 'Óptimo'
                            WHEN value <= 24 THEN 'Normal'
                            WHEN value <= 29 THEN 'Alto'
                            ELSE 'Obesidad'
                        END
                    ELSE
                        CASE
                            WHEN value <= 23 THEN 'Óptimo'
                            WHEN value <= 31 THEN 'Normal'
                            WHEN value <= 37 THEN 'Alto'
                            ELSE 'Obesidad'
                        END
                END AS grasa_category,
                value
            FROM src
        )
        INSERT INTO app.biometria_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
        SELECT
            metric_date,
            'grasa_distribution' AS metric_key,
            sex_prefix || '_' || grasa_category AS dimension_key,
            COUNT(*) AS total_count,
            SUM(value) AS total_value,
            NOW() AS last_updated_at
        FROM base
        GROUP BY metric_date, sex_prefix, grasa_category
        ON CONFLICT (metric_date, metric_key, dimension_key)
        DO UPDATE SET total_count = EXCLUDED.total_count, total_value = EXCLUDED.total_value, last_updated_at = NOW();

        -- 3. Glucosa en ayunas: distribución ADA por día. La dimensión
        -- "Sin dato" (vitals sin glucosa) es SOLO de evento y no se toca aquí.
        INSERT INTO app.biometria_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
        SELECT
            DATE(cm.observed_at) AS metric_date,
            'glucosa_distribution' AS metric_key,
            CASE
                WHEN cm.value < 100 THEN 'Normal'
                WHEN cm.value < 126 THEN 'Prediabetes'
                ELSE 'Elevada'
            END AS dimension_key,
            COUNT(*) AS total_count,
            SUM(cm.value) AS total_value,
            NOW() AS last_updated_at
        FROM app.clinical_measurements cm
        JOIN app.measurement_metrics mm ON cm.metric_id = mm.id
        WHERE mm.code = 'glucose_fasting'
          AND cm.value > 0
        GROUP BY DATE(cm.observed_at), CASE
            WHEN cm.value < 100 THEN 'Normal'
            WHEN cm.value < 126 THEN 'Prediabetes'
            ELSE 'Elevada'
        END
        ON CONFLICT (metric_date, metric_key, dimension_key)
        DO UPDATE SET total_count = EXCLUDED.total_count, total_value = EXCLUDED.total_value, last_updated_at = NOW();

        -- 4. Promedio comunitario por métrica (imc/grasa/glucosa): conteo +
        -- suma del valor para calcular el promedio en O(1) (total_value/total_count).
        INSERT INTO app.biometria_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
        SELECT
            DATE(cm.observed_at) AS metric_date,
            'community_avg' AS metric_key,
            CASE mm.code
                WHEN 'bmi'             THEN 'imc'
                WHEN 'body_fat'        THEN 'grasa'
                WHEN 'glucose_fasting' THEN 'glucosa'
            END AS dimension_key,
            COUNT(*) AS total_count,
            SUM(cm.value) AS total_value,
            NOW() AS last_updated_at
        FROM app.clinical_measurements cm
        JOIN app.measurement_metrics mm ON cm.metric_id = mm.id
        WHERE mm.code IN ('bmi', 'body_fat', 'glucose_fasting')
          AND cm.value > 0
        GROUP BY DATE(cm.observed_at), mm.code
        ON CONFLICT (metric_date, metric_key, dimension_key)
        DO UPDATE SET total_count = EXCLUDED.total_count, total_value = EXCLUDED.total_value, last_updated_at = NOW();
        """;

    /// <summary>Ejecuta el recomputo completo (backfill) del rollup de Biometría.</summary>
    public static Task<int> RecomputeAllAsync(
        AppDbContext dbContext,
        CancellationToken ct = default
    ) => dbContext.Database.ExecuteSqlRawAsync(RecomputeSql, ct);
}
