using System;
using System.Threading;
using System.Threading.Tasks;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// SQL de recomputo (idempotente) del rollup snapshot geográfico del dashboard
/// de Tests de Salud (<c>app.health_test_geo_rollups</c>). Un mismo statement
/// sirve para el backfill completo (<paramref name="cityId"/> = null, además
/// limpia filas huérfanas) y para el recomputo incremental de una ciudad
/// (processor de eventos). Semántica idéntica a la agregación en memoria que
/// reemplaza: peor severidad por paciente sobre resultados tipo score, alto
/// riesgo = high/critical, promedio de score por ciudad = media de promedios
/// por paciente.
/// </summary>
public static class HealthTestGeoRollupSql
{
    public const string RecomputeSql = """
        WITH ps AS (
            SELECT e.patient_id AS patient_id,
                   AVG(r.value) AS avg_score,
                   MAX(CASE r.severity
                       WHEN 'critical' THEN 3
                       WHEN 'high'     THEN 2
                       WHEN 'moderate' THEN 1
                       ELSE 0 END) AS worst_ord
            FROM app.health_test_results r
            JOIN app.health_test_evaluations e ON e.id = r.evaluation_id
            WHERE r.result_type = 'score'
              AND (@cityId::uuid IS NULL
                   OR e.patient_id IN (
                        SELECT p2.id FROM app.patient_profiles p2
                        WHERE p2.city_id = @cityId::uuid AND p2.deleted_at IS NULL))
            GROUP BY e.patient_id
        ),
        pa AS (
            SELECT al.patient_id AS patient_id, COUNT(*) AS active_count
            FROM app.health_test_alerts al
            WHERE al.status = 'active'
              AND (@cityId::uuid IS NULL
                   OR al.patient_id IN (
                        SELECT p2.id FROM app.patient_profiles p2
                        WHERE p2.city_id = @cityId::uuid AND p2.deleted_at IS NULL))
            GROUP BY al.patient_id
        )
        INSERT INTO app.health_test_geo_rollups
            (city_id, state_code, city_name, patients_count, evaluated_count,
             high_risk_count, active_alerts_count, avg_score_sum, avg_score_count, updated_at)
        SELECT c.id,
               s.code,
               c.name,
               COUNT(p.id),
               COUNT(ps.patient_id),
               COUNT(ps.patient_id) FILTER (WHERE ps.worst_ord >= 2),
               COALESCE(SUM(pa.active_count), 0),
               COALESCE(SUM(ps.avg_score), 0),
               COUNT(ps.patient_id),
               NOW()
        FROM app.patient_profiles p
        JOIN app.cities c ON c.id = p.city_id
        JOIN app.states s ON s.id = c.state_id
        LEFT JOIN ps ON ps.patient_id = p.id
        LEFT JOIN pa ON pa.patient_id = p.id
        WHERE p.deleted_at IS NULL
          AND (@cityId::uuid IS NULL OR p.city_id = @cityId::uuid)
        GROUP BY c.id, s.code, c.name
        ON CONFLICT (city_id) DO UPDATE SET
            state_code = EXCLUDED.state_code,
            city_name = EXCLUDED.city_name,
            patients_count = EXCLUDED.patients_count,
            evaluated_count = EXCLUDED.evaluated_count,
            high_risk_count = EXCLUDED.high_risk_count,
            active_alerts_count = EXCLUDED.active_alerts_count,
            avg_score_sum = EXCLUDED.avg_score_sum,
            avg_score_count = EXCLUDED.avg_score_count,
            updated_at = NOW();

        -- Filas huérfanas: la ciudad ya no tiene pacientes activos.
        DELETE FROM app.health_test_geo_rollups g
        WHERE (@cityId::uuid IS NULL OR g.city_id = @cityId::uuid)
          AND NOT EXISTS (
                SELECT 1 FROM app.patient_profiles p
                WHERE p.city_id = g.city_id AND p.deleted_at IS NULL);
        """;

    /// <summary>Parámetro uuid nullable: null → NULL (modo backfill completo).</summary>
    private static NpgsqlParameter CityParam(Guid? cityId) =>
        new("@cityId", NpgsqlDbType.Uuid) { Value = (object?)cityId ?? DBNull.Value };

    /// <summary>Ejecuta el recomputo completo (backfill) del rollup geo.</summary>
    public static Task<int> RecomputeAllAsync(
        AppDbContext dbContext,
        CancellationToken ct = default
    ) => dbContext.Database.ExecuteSqlRawAsync(RecomputeSql, [CityParam(null)], ct);

    /// <summary>Ejecuta el recomputo de una sola ciudad (incremental).</summary>
    public static Task<int> RecomputeCityAsync(
        AppDbContext dbContext,
        Guid cityId,
        CancellationToken ct = default
    ) => dbContext.Database.ExecuteSqlRawAsync(RecomputeSql, [CityParam(cityId)], ct);
}
