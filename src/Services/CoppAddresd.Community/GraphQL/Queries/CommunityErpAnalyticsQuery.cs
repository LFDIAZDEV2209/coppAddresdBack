using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Types;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Queries;

/// <summary>Punto diario de la distribución de posts por tipo (etiqueta textual del rollup).</summary>
public sealed record ErpPostTypeStat
{
    /// <summary>Dimensión del rollup: "texto", "imagen", "video", "poll", "logro", "general" (las encuestas se normalizan a "poll").</summary>
    public string Type { get; init; } = default!;

    /// <summary>Total de posts de ese tipo en el rango.</summary>
    public long Count { get; init; }
}

/// <summary>
/// Analítica ERP del dashboard de comunidad servida desde el rollup pre-agregado
/// (<c>community.community_daily_metrics</c>): serie diaria, distribución por tipo
/// y horas pico. Si el rollup no tiene filas en el rango (sistema recién iniciado),
/// cae al OLTP como fallback.
/// </summary>
public sealed record ErpCommunityAnalytics
{
    /// <summary>Serie temporal diaria (posts, comentarios, reacciones) del rango.</summary>
    public IReadOnlyList<ActivityDay> DailySeries { get; init; } = [];

    /// <summary>Distribución de posts por tipo en el rango.</summary>
    public IReadOnlyList<ErpPostTypeStat> PostTypes { get; init; } = [];

    /// <summary>Horas pico de actividad (posts + comentarios + reposts) en el rango.</summary>
    public IReadOnlyList<PeakHour> PeakHours { get; init; } = [];
}

/// <summary>
/// Consultas de analítica para el dashboard ERP de comunidad (Dashboard #5).
/// Estrategia de lectura: rollup pre-agregado primero (O(1) sobre
/// <c>community.community_daily_metrics</c>); si no hay filas en el rango,
/// COUNT(*) sobre las tablas OLTP como fallback.
/// </summary>
[ExtendObjectType(typeof(CommunityQuery))]
public sealed class CommunityErpAnalyticsQuery
{
    private const string PostsKey = "posts_count";
    private const string CommentsKey = "comments_count";
    private const string LikesKey = "likes_count";
    private const string RepostsKey = "reposts_count";
    private const string HourlyKey = "hourly_activity";
    private const string TotalDimension = "total";

    /// <summary>
    /// Analítica del dashboard ERP de comunidad. Rango por defecto: últimos 30 días.
    /// Lee el rollup pre-agregado; si está vacío, calcula desde el OLTP (fallback).
    /// </summary>
    [Authorize(Policy = "Community.View")]
    public async Task<ErpCommunityAnalytics> CommunityErpAnalytics(
        [Service] CommunityDbContext db,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? toDate.AddDays(-29);

        // 1. Lectura desde el rollup (O(1) sobre la tabla pre-agregada).
        var metrics = await db.CommunityDailyMetrics.AsNoTracking()
            .Where(x => x.MetricDate >= fromDate && x.MetricDate <= toDate)
            .ToListAsync(ct);

        // 2. Fallback a OLTP cuando el rollup no tiene filas en el rango.
        if (metrics.Count == 0)
            return await ComputeFromOltpAsync(db, fromDate, toDate, ct);

        return new ErpCommunityAnalytics
        {
            DailySeries = BuildDailySeries(metrics),
            PostTypes = BuildPostTypeDistribution(metrics),
            PeakHours = BuildPeakHours(metrics),
        };
    }

    // ─── Transformaciones del rollup ─────────────────────────────────────

    private static IReadOnlyList<ActivityDay> BuildDailySeries(IReadOnlyCollection<CommunityDailyMetric> metrics)
    {
        var totals = metrics
            .Where(x => x.DimensionKey == TotalDimension
                && x.MetricKey is PostsKey or CommentsKey or LikesKey)
            .GroupBy(x => x.MetricDate)
            .Select(g => new
            {
                Date = g.Key,
                Posts = g.Where(x => x.MetricKey == PostsKey).Sum(x => x.TotalCount),
                Comments = g.Where(x => x.MetricKey == CommentsKey).Sum(x => x.TotalCount),
                Likes = g.Where(x => x.MetricKey == LikesKey).Sum(x => x.TotalCount),
            })
            .ToList();

        return totals
            .OrderBy(x => x.Date)
            .Select(x => new ActivityDay
            {
                // Etiqueta "MMM d" (ej. "sep 4"): el día del mes a secas colisiona
                // cuando el rango de 30 días cruza de mes (dos etiquetas "1", etc.).
                Dia = x.Date.ToString("MMM d"),
                Posts = (int)x.Posts,
                Comentarios = (int)x.Comments,
                Reacciones = (int)x.Likes,
            })
            .ToList();
    }

    private static IReadOnlyList<ErpPostTypeStat> BuildPostTypeDistribution(IReadOnlyCollection<CommunityDailyMetric> metrics)
        => metrics
            .Where(x => x.MetricKey == PostsKey && x.DimensionKey != TotalDimension)
            .GroupBy(x => x.DimensionKey)
            .Select(g => new ErpPostTypeStat { Type = g.Key, Count = g.Sum(x => x.TotalCount) })
            .OrderByDescending(x => x.Count)
            .ToList();

    private static IReadOnlyList<PeakHour> BuildPeakHours(IReadOnlyCollection<CommunityDailyMetric> metrics)
        => metrics
            .Where(x => x.MetricKey == HourlyKey)
            .GroupBy(x => x.DimensionKey)
            .Select(g => new PeakHour
            {
                Hora = int.Parse(g.Key),
                Count = (int)g.Sum(x => x.TotalCount),
            })
            .OrderBy(x => x.Hora)
            .ToList();

    // ─── Fallback OLTP (sistema recién iniciado, rollup vacío) ──────────

    private static async Task<ErpCommunityAnalytics> ComputeFromOltpAsync(
        CommunityDbContext db, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Consultas secuenciales (EF Core no permite operaciones concurrentes sobre un mismo DbContext).
        var postsPerDay = await db.Posts
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusive)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var commentsPerDay = await db.Comments
            .Where(c => c.CreatedAt >= fromUtc && c.CreatedAt < toExclusive)
            .GroupBy(c => c.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var likesPerDay = await db.Likes
            .Where(l => l.CreatedAt >= fromUtc && l.CreatedAt < toExclusive)
            .GroupBy(l => l.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var postTypes = await db.Posts
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusive)
            .GroupBy(p => p.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var postsByHour = await db.Posts
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusive)
            .GroupBy(p => p.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var commentsByHour = await db.Comments
            .Where(c => c.CreatedAt >= fromUtc && c.CreatedAt < toExclusive)
            .GroupBy(c => c.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var repostsByHour = await db.Reposts
            .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < toExclusive)
            .GroupBy(r => r.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var days = postsPerDay
            .Select(x => x.Day)
            .Union(commentsPerDay.Select(x => x.Day))
            .Union(likesPerDay.Select(x => x.Day))
            .OrderBy(x => x)
            .ToList();

        var postsByDay = postsPerDay.ToDictionary(x => x.Day, x => x.Count);
        var commentsByDay = commentsPerDay.ToDictionary(x => x.Day, x => x.Count);
        var likesByDay = likesPerDay.ToDictionary(x => x.Day, x => x.Count);

        var dailySeries = days
            .Select(day => new ActivityDay
            {
                Dia = day.ToString("MMM d"),
                Posts = postsByDay.GetValueOrDefault(day),
                Comentarios = commentsByDay.GetValueOrDefault(day),
                Reacciones = likesByDay.GetValueOrDefault(day),
            })
            .ToList();

        var distribution = postTypes
            .Select(x => new ErpPostTypeStat
            {
                Type = PostTypeToDimensionKey(x.Type),
                Count = x.Count,
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // hourly_activity del rollup acumula posts + comentarios + reposts.
        var hourly = new Dictionary<int, int>();
        foreach (var (hour, count) in postsByHour.Select(x => (x.Hour, x.Count)))
            hourly[hour] = hourly.GetValueOrDefault(hour) + count;
        foreach (var (hour, count) in commentsByHour.Select(x => (x.Hour, x.Count)))
            hourly[hour] = hourly.GetValueOrDefault(hour) + count;
        foreach (var (hour, count) in repostsByHour.Select(x => (x.Hour, x.Count)))
            hourly[hour] = hourly.GetValueOrDefault(hour) + count;

        var peakHours = hourly
            .OrderBy(x => x.Key)
            .Select(x => new PeakHour { Hora = x.Key, Count = x.Value })
            .ToList();

        return new ErpCommunityAnalytics
        {
            DailySeries = dailySeries,
            PostTypes = distribution,
            PeakHours = peakHours,
        };
    }

    /// <summary>
    /// Mapea el enum PostType a la dimensión textual del rollup. Encuesta se
    /// normaliza a "poll" para coincidir con la dimensión que escribe
    /// <c>CreatePollPost</c>; el resto usa el nombre del enum en minúsculas.
    /// </summary>
    private static string PostTypeToDimensionKey(PostType? type) => type switch
    {
        PostType.Encuesta => "poll",
        null => "general",
        _ => type.Value.ToString().ToLowerInvariant(),
    };
}