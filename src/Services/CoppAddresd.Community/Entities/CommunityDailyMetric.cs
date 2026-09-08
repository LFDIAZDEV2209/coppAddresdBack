namespace CoppAddresd.Community.Entities;

/// <summary>
/// Rollup diario pre-agregado para la analítica del dashboard ERP de Comunidad.
/// Clave: (metric_date, metric_key, dimension_key).
/// El rollup es intencionalmente global — no tiene columna de tenant —: agrega
/// la actividad de toda la comunidad, no por organización.
/// </summary>
public class CommunityDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public string MetricKey { get; set; } = "";       // "posts_count", "comments_count", "likes_count", "reposts_count", "hourly_activity"
    public string DimensionKey { get; set; } = "";    // "total"; tipo de post ("texto", "imagen", "video", "poll", "logro", "general"); hora "0"–"23"
    public long TotalCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}