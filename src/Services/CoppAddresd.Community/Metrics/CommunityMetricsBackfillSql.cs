using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.Metrics;

/// <summary>
/// SQL de reconstrucción autoritativa (idempotente) del rollup de métricas del
/// dashboard ERP de Comunidad (<c>community.community_daily_metrics</c>). El
/// OLTP es la única fuente de verdad: el rollup es descartable y siempre se
/// puede reconstruir desde él.
///
/// Replica exactamente la semántica del processor incremental
/// (<see cref="CommunityMetricsProcessorHostedService"/>):
/// <list type="bullet">
///   <item><c>posts_count/total</c> y <c>posts_count/&lt;tipo&gt;</c> —
///   Texto→texto, Imagen→imagen, Video→video, Encuesta→poll, Logro→logro,
///   NULL→general. Incluye posts soft-deleted: el rollup nunca decrementa al
///   eliminar (el evento original ya contó).</item>
///   <item><c>comments_count/total</c> — incluye comentarios soft-deleted
///   (misma regla que los posts).</item>
///   <item><c>likes_count/total</c> — solo las filas actuales de
///   <c>community.likes</c> (al quitar el like la fila se borra físicamente,
///   por eso el recálculo puede reducirlo).</item>
///   <item><c>reposts_count/total</c> — solo las filas actuales de
///   <c>community.reposts</c>.</item>
///   <item><c>hourly_activity/&lt;hora&gt;</c> — posts + comentarios + reposts
///   por hora UTC. La dimensión es la hora SIN padding (<c>"0"</c>..<c>"23"</c>),
///   igual que <c>HourOfDay.ToString()</c> del processor — nunca <c>"05"</c>.</item>
/// </list>
///
/// Por qué DELETE + INSERT (y no un upsert solo de sobrescritura):
/// <c>likes_count</c>, <c>reposts_count</c> y <c>hourly_activity</c> se
/// decrementan en vivo (unlike/unrepost), así que un rebuild que solo
/// sobrescriba celdas dejaría filas obsoletas en días cuya actividad actual ya
/// no existe. Borrar las 5 claves y re-insertar deja la tabla exactamente igual
/// al OLTP (las celdas con conteo 0 desaparecen).
///
/// El comando agrupa todos los statements (el DELETE + los INSERT); PostgreSQL
/// los ejecuta en una transacción implícita y, cuando el caller ya abrió una
/// transacción explícita, participan de ella.
/// </summary>
public static class CommunityMetricsBackfillSql
{
    public const string PostsKey = "posts_count";
    public const string CommentsKey = "comments_count";
    public const string LikesKey = "likes_count";
    public const string RepostsKey = "reposts_count";
    public const string HourlyKey = "hourly_activity";

    public const string TotalDimension = "total";

    /// <summary>Claves administradas por el rollup (las que el rebuild reemplaza).</summary>
    public static readonly string[] MetricKeys =
        [PostsKey, CommentsKey, LikesKey, RepostsKey, HourlyKey];

    public const string RecomputeSql = """
        -- Rebuild autoritativo: el rollup es descartable, el OLTP es la fuente de verdad.
        DELETE FROM community.community_daily_metrics
        WHERE metric_key IN ('posts_count', 'comments_count', 'likes_count', 'reposts_count', 'hourly_activity');

        -- posts_count/total — incluye soft-deleted (el rollup nunca decrementa al eliminar).
        INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
        SELECT (p.created_at AT TIME ZONE 'UTC')::date, 'posts_count', 'total', COUNT(*), NOW()
        FROM community.posts AS p
        GROUP BY 1;

        -- posts_count/<tipo> — mismo mapeo que ToDimensionKey del processor:
        -- Encuesta → 'poll', NULL → 'general', el resto el nombre del enum en minúsculas.
        INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
        SELECT (p.created_at AT TIME ZONE 'UTC')::date,
               'posts_count',
               CASE
                   WHEN p.type = 'Encuesta' THEN 'poll'
                   WHEN p.type IS NULL THEN 'general'
                   ELSE LOWER(p.type)
               END,
               COUNT(*),
               NOW()
        FROM community.posts AS p
        GROUP BY 1, 3;

        -- comments_count/total — incluye soft-deleted.
        INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
        SELECT (c.created_at AT TIME ZONE 'UTC')::date, 'comments_count', 'total', COUNT(*), NOW()
        FROM community.comments AS c
        GROUP BY 1;

        -- likes_count/total — filas actuales (el unlike las elimina físicamente).
        INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
        SELECT (l.created_at AT TIME ZONE 'UTC')::date, 'likes_count', 'total', COUNT(*), NOW()
        FROM community.likes AS l
        GROUP BY 1;

        -- reposts_count/total — filas actuales (el unrepost las elimina físicamente).
        INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
        SELECT (r.created_at AT TIME ZONE 'UTC')::date, 'reposts_count', 'total', COUNT(*), NOW()
        FROM community.reposts AS r
        GROUP BY 1;

        -- hourly_activity/<hora> — posts + comentarios + reposts por hora UTC.
        -- Dimensión SIN padding: "0".."23" (EXTRACT::int::text), nunca "00".."23".
        INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
        SELECT metric_date, 'hourly_activity', hour_key, SUM(activity), NOW()
        FROM (
            SELECT (p.created_at AT TIME ZONE 'UTC')::date AS metric_date,
                   (EXTRACT(HOUR FROM (p.created_at AT TIME ZONE 'UTC')))::int::text AS hour_key,
                   COUNT(*) AS activity
            FROM community.posts AS p
            GROUP BY 1, 2
            UNION ALL
            SELECT (c.created_at AT TIME ZONE 'UTC')::date,
                   (EXTRACT(HOUR FROM (c.created_at AT TIME ZONE 'UTC')))::int::text,
                   COUNT(*)
            FROM community.comments AS c
            GROUP BY 1, 2
            UNION ALL
            SELECT (r.created_at AT TIME ZONE 'UTC')::date,
                   (EXTRACT(HOUR FROM (r.created_at AT TIME ZONE 'UTC')))::int::text,
                   COUNT(*)
            FROM community.reposts AS r
            GROUP BY 1, 2
        ) AS hourly
        GROUP BY metric_date, hour_key;
        """;

    /// <summary>
    /// Reconstruye íntegramente las 5 claves del rollup desde el OLTP.
    /// Idempotente y autoritativo: reemplaza el contenido de esas claves (el
    /// DELETE + INSERT corre en la transacción del caller o en la implícita del
    /// comando único).
    /// </summary>
    public static Task<int> RecomputeAllAsync(
        CommunityDbContext dbContext,
        CancellationToken ct = default
    ) => dbContext.Database.ExecuteSqlRawAsync(RecomputeSql, ct);
}
