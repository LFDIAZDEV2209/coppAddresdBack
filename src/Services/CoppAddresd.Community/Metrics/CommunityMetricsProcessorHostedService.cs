using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Community.Metrics;

public sealed class CommunityMetricsProcessorHostedService(
    ICommunityMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<CommunityMetricsProcessorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador en segundo plano de métricas de Comunidad.");

        await foreach (var evt in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();

                switch (evt)
                {
                    case PostCreatedMetricEvent e:
                        await ProcessPostCreatedAsync(db, e, stoppingToken);
                        break;
                    case CommentCreatedMetricEvent e:
                        await ProcessCommentCreatedAsync(db, e, stoppingToken);
                        break;
                    case LikeAddedMetricEvent e:
                        await ProcessLikeAddedAsync(db, e, stoppingToken);
                        break;
                    case LikeRemovedMetricEvent e:
                        await ProcessLikeRemovedAsync(db, e, stoppingToken);
                        break;
                    case RepostCreatedMetricEvent e:
                        await ProcessRepostCreatedAsync(db, e, stoppingToken);
                        break;
                    case RepostRemovedMetricEvent e:
                        await ProcessRepostRemovedAsync(db, e, stoppingToken);
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error procesando evento de métrica de comunidad: {@Event}", evt);
            }
        }
    }

    // --- Implementaciones de upsert ---

    private static async Task ProcessPostCreatedAsync(CommunityDbContext db, PostCreatedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'posts_count', 'total', 1, NOW()),
                (@p0, 'posts_count', @p1, 1, NOW()),
                (@p0, 'hourly_activity', @p2, 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql,
            [e.MetricDate, e.PostType, e.HourOfDay.ToString()], ct);
    }

    private static async Task ProcessCommentCreatedAsync(CommunityDbContext db, CommentCreatedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'comments_count', 'total', 1, NOW()),
                (@p0, 'hourly_activity', @p1, 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql,
            [e.MetricDate, e.HourOfDay.ToString()], ct);
    }

    private static async Task ProcessLikeAddedAsync(CommunityDbContext db, LikeAddedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES (@p0, 'likes_count', 'total', 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql, [e.MetricDate], ct);
    }

    private static async Task ProcessLikeRemovedAsync(CommunityDbContext db, LikeRemovedMetricEvent e, CancellationToken ct)
    {
        // Decrementa el bucket del día ORIGINAL del like (el día en que se agregó),
        // transportado en e.MetricDate — nunca el día en que se removió.
        const string sql = """
            UPDATE community.community_daily_metrics
            SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
            WHERE metric_date = @p0 AND metric_key = 'likes_count' AND dimension_key = 'total';
            """;

        await db.Database.ExecuteSqlRawAsync(sql, [e.MetricDate], ct);
    }

    private static async Task ProcessRepostCreatedAsync(CommunityDbContext db, RepostCreatedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'reposts_count', 'total', 1, NOW()),
                (@p0, 'hourly_activity', @p1, 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql,
            [e.MetricDate, e.HourOfDay.ToString()], ct);
    }

    private static async Task ProcessRepostRemovedAsync(CommunityDbContext db, RepostRemovedMetricEvent e, CancellationToken ct)
    {
        // Decrementa los buckets de la fecha/hora ORIGINALES del repost
        // (reposts_count/total y hourly_activity/<hora>), nunca negativos.
        const string sqlReposts = """
            UPDATE community.community_daily_metrics
            SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
            WHERE metric_date = @p0 AND metric_key = 'reposts_count' AND dimension_key = 'total';
            """;

        const string sqlHourly = """
            UPDATE community.community_daily_metrics
            SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
            WHERE metric_date = @p0 AND metric_key = 'hourly_activity' AND dimension_key = @p1;
            """;

        await db.Database.ExecuteSqlRawAsync(sqlReposts, [e.MetricDate], ct);
        await db.Database.ExecuteSqlRawAsync(sqlHourly, [e.MetricDate, e.HourOfDay.ToString()], ct);
    }
}