using CoppAddresd.Community.GraphQL.Types;

namespace CoppAddresd.Community.Entities;

/// <summary>
/// Agregador puro y determinista para el dashboard de la comunidad ERP.
/// Calcula KPIs, series temporales, distribuciones y tendencias a partir de
/// colecciones en memoria (sin EF/DbContext) para facilitar las pruebas unitarias.
/// </summary>
public static class DashboardAggregator
{
    /// <summary>
    /// Calcula las estadísticas completas del dashboard a partir de los datos
    /// cargados desde la base de datos (sequential chained queries).
    /// </summary>
    /// <param name="profiles">Todos los perfiles (DbSet sin filtro).</param>
    /// <param name="posts">Publicaciones activas (DeletedAt == null, o todas; el aggregator filtra).</param>
    /// <param name="comments">Comentarios (el aggregator filtra DeletedAt == null).</param>
    /// <param name="likes">Todos los likes.</param>
    /// <param name="feedEvents">Todos los eventos del feed.</param>
    /// <param now>Instante de referencia (DateTime.UtcNow del caller).</param>
    public static DashboardStats Compute(
        IReadOnlyCollection<Profile> profiles,
        IReadOnlyCollection<Post> posts,
        IReadOnlyCollection<Comment> comments,
        IReadOnlyCollection<Like> likes,
        IReadOnlyCollection<FeedEvent> feedEvents,
        DateTime now)
    {
        var utcNow = now.ToUniversalTime();
        var today = utcNow.Date;

        // --- Miembros activos ---
        var activeProfiles = profiles.Where(p => p.Status == ProfileStatus.Active).ToList();
        var activeCount = activeProfiles.Count;

        // --- Publicaciones de este mes (solo no eliminadas) ---
        var currentMonthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var activePosts = posts.Where(p => p.DeletedAt == null).ToList();
        var postsThisMonth = activePosts.Count(p => p.CreatedAt >= currentMonthStart);

        // --- Publicaciones del mes anterior ---
        var postsPreviousMonth = activePosts.Count(p => p.CreatedAt >= previousMonthStart && p.CreatedAt < currentMonthStart);

        // --- Participación semanal (last 7d): perfiles con al menos 1 actividad ---
        var window7d = utcNow.AddDays(-7);
        var weeklyActiveIds = ComputeActiveProfileIdsInWindow(
            activeProfiles, posts, comments, likes, feedEvents, window7d, utcNow);
        var weeklyActiveCount = weeklyActiveIds.Count;
        var participationRate = activeCount > 0
            ? Math.Round(weeklyActiveCount * 100.0 / activeCount, 1)
            : 0.0;

        // --- Participación semana anterior (7-14d atrás) ---
        var windowPrev7dStart = utcNow.AddDays(-14);
        var windowPrev7dEnd = utcNow.AddDays(-7);
        var prevWeeklyActiveIds = ComputeActiveProfileIdsInWindow(
            activeProfiles, posts, comments, likes, feedEvents, windowPrev7dStart, windowPrev7dEnd);
        var prevParticipationRate = activeCount > 0
            ? Math.Round(prevWeeklyActiveIds.Count * 100.0 / activeCount, 1)
            : 0.0;

        // --- Inactivos >7d (activos sin actividad en 7d) ---
        var inactiveOver7Days = activeProfiles.Count(p =>
            IsInactiveBefore(p, utcNow.AddDays(-7)));

        // --- Inactivos >7d que estaban inactivos hace 30 días (para delta) ---
        var inactive30dAgo = activeProfiles.Count(p =>
            IsInactiveBefore(p, utcNow.AddDays(-37)));

        // --- Inactivos en riesgo (Alto) ---
        var inactiveAtRisk = activeProfiles.Count(p =>
            IsInactiveBefore(p, utcNow.AddDays(-7))
            && CommunityStats.ComputeRiskLevel(p.LastPostAt, p.LastActiveAt) == RiskLevel.Alto);

        // --- KPI Trends ---
        var activeInLast30d = CountActiveInWindow(activeProfiles, utcNow.AddDays(-30), utcNow);
        var activeInPrior30d = CountActiveInWindow(activeProfiles, utcNow.AddDays(-60), utcNow.AddDays(-30));
        var kpiTrends = new DashboardKpiTrends
        {
            ActiveMembers = activeInPrior30d > 0
                ? Math.Round((activeInLast30d - activeInPrior30d) * 100.0 / activeInPrior30d, 1)
                : 0.0,
            PostsThisMonth = postsPreviousMonth > 0
                ? Math.Round((postsThisMonth - postsPreviousMonth) * 100.0 / postsPreviousMonth, 1)
                : 0.0,
            ParticipationRate = Math.Round(participationRate - prevParticipationRate, 1),
            InactiveOver7Days = inactive30dAgo > 0
                ? Math.Round((inactiveOver7Days - inactive30dAgo) * 100.0 / inactive30dAgo, 1)
                : 0.0,
        };

        // --- Activity Series: últimos 30 días (now-29d..now) ---
        var activitySeries = BuildActivitySeries(activePosts, comments, likes, today);

        // --- Post Types: distribución por tipo este mes ---
        var postTypes = BuildPostTypeDistribution(activePosts, currentMonthStart);

        // --- Peak Hours: actividad (posts+comments+likes) por hora en últimos 30 días ---
        var peakHours = BuildPeakHours(activePosts, comments, likes, utcNow.AddDays(-30));

        // --- Diagnosis Participation ---
        var diagnosisParticipation = BuildDiagnosisParticipation(
            activeProfiles, posts, comments, likes, feedEvents, utcNow);

        return new DashboardStats
        {
            ActiveMembers = activeCount,
            PostsThisMonth = postsThisMonth,
            ParticipationRate = participationRate,
            InactiveOver7Days = inactiveOver7Days,
            InactiveAtRisk = inactiveAtRisk,
            KpiTrends = kpiTrends,
            ActivitySeries = activitySeries,
            PostTypes = postTypes,
            PeakHours = peakHours,
            DiagnosisParticipation = diagnosisParticipation,
        };
    }

    /// <summary>
    /// Determina si un perfil estaba inactivo antes del umbral dado:
    /// max(LastPostAt, LastActiveAt) es null o anterior al umbral.
    /// </summary>
    private static bool IsInactiveBefore(Profile profile, DateTime threshold)
    {
        var last = profile.LastPostAt.HasValue && profile.LastActiveAt.HasValue
            ? (profile.LastPostAt.Value > profile.LastActiveAt.Value ? profile.LastPostAt.Value : profile.LastActiveAt.Value)
            : profile.LastPostAt ?? profile.LastActiveAt;

        if (last is null)
            return true; // Sin actividad ⇒ inactivo

        return last.Value.UtcDateTime < threshold;
    }

    /// <summary>Cuenta perfiles activos que tuvieron al menos una actividad en la ventana [from, to).</summary>
    private static int CountActiveInWindow(
        IReadOnlyCollection<Profile> activeProfiles,
        DateTime from,
        DateTime to)
    {
        return activeProfiles.Count(p =>
        {
            var last = p.LastPostAt.HasValue && p.LastActiveAt.HasValue
                ? (p.LastPostAt.Value > p.LastActiveAt.Value ? p.LastPostAt.Value : p.LastActiveAt.Value)
                : p.LastPostAt ?? p.LastActiveAt;
            if (last is null) return false;
            var utc = last.Value.UtcDateTime;
            return utc >= from && utc < to;
        });
    }

    /// <summary>
    /// IDs de perfiles activos con al menos una actividad (Post/Comment/Like/FeedEvent)
    /// en la ventana [from, to).
    /// </summary>
    private static HashSet<Guid> ComputeActiveProfileIdsInWindow(
        IReadOnlyCollection<Profile> activeProfiles,
        IReadOnlyCollection<Post> posts,
        IReadOnlyCollection<Comment> comments,
        IReadOnlyCollection<Like> likes,
        IReadOnlyCollection<FeedEvent> feedEvents,
        DateTime from,
        DateTime to)
    {
        var activeIds = new HashSet<Guid>(activeProfiles.Select(p => p.Id));
        var result = new HashSet<Guid>();

        foreach (var p in posts.Where(p => activeIds.Contains(p.ProfileId) && p.CreatedAt >= from && p.CreatedAt < to))
            result.Add(p.ProfileId);

        foreach (var c in comments.Where(c => activeIds.Contains(c.ProfileId) && c.CreatedAt >= from && c.CreatedAt < to))
            result.Add(c.ProfileId);

        foreach (var l in likes.Where(l => activeIds.Contains(l.ProfileId) && l.CreatedAt >= from && l.CreatedAt < to))
            result.Add(l.ProfileId);

        foreach (var f in feedEvents)
        {
            if (f.ProfileId is Guid pid && activeIds.Contains(pid) && f.CreatedAt >= from && f.CreatedAt < to)
                result.Add(pid);
        }

        return result;
    }

    /// <summary>
    /// Serie diaria de actividad de los últimos 30 días (now-29d..now). Zero-padded.
    /// Cada entrada: día del mes, posts, comentarios, reacciones.
    /// </summary>
    private static IReadOnlyList<ActivityDay> BuildActivitySeries(
        IReadOnlyCollection<Post> posts,
        IReadOnlyCollection<Comment> comments,
        IReadOnlyCollection<Like> likes,
        DateTime today)
    {
        var from = today.AddDays(-29);

        // Diccionarios de conteo por día UTC
        var postsByDay = posts
            .Where(p => p.CreatedAt >= from)
            .GroupBy(p => p.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var commentsByDay = comments
            .Where(c => c.DeletedAt == null && c.CreatedAt >= from)
            .GroupBy(c => c.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var likesByDay = likes
            .Where(l => l.CreatedAt >= from)
            .GroupBy(l => l.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<ActivityDay>(30);
        for (var i = 0; i < 30; i++)
        {
            var day = from.AddDays(i);
            result.Add(new ActivityDay
            {
                Dia = day.Day,
                Posts = postsByDay.GetValueOrDefault(day.Date, 0),
                Comentarios = commentsByDay.GetValueOrDefault(day.Date, 0),
                Reacciones = likesByDay.GetValueOrDefault(day.Date, 0),
            });
        }

        return result;
    }

    /// <summary>
    /// Distribución de publicaciones por PostType (este mes, no eliminadas).
    /// Incluye los 5 valores del enum, con 0 si ninguno.
    /// </summary>
    private static IReadOnlyList<PostTypeCount> BuildPostTypeDistribution(
        IReadOnlyCollection<Post> posts,
        DateTime currentMonthStart)
    {
        var counts = posts
            .Where(p => p.CreatedAt >= currentMonthStart)
            .GroupBy(p => p.Type ?? PostType.Texto)
            .ToDictionary(g => g.Key, g => g.Count());

        return Enum.GetValues<PostType>()
            .Select(t => new PostTypeCount { Type = t, Count = counts.GetValueOrDefault(t, 0) })
            .ToList();
    }

    /// <summary>
    /// Actividad (posts + comments + likes) por hora del día (UTC) en los últimos 30 días.
    /// 24 entries, hora 0..23, zero-padded.
    /// </summary>
    private static IReadOnlyList<PeakHour> BuildPeakHours(
        IReadOnlyCollection<Post> posts,
        IReadOnlyCollection<Comment> comments,
        IReadOnlyCollection<Like> likes,
        DateTime from)
    {
        var hourCounts = new int[24];

        foreach (var p in posts.Where(p => p.CreatedAt >= from))
            hourCounts[p.CreatedAt.Hour]++;

        foreach (var c in comments.Where(c => c.DeletedAt == null && c.CreatedAt >= from))
            hourCounts[c.CreatedAt.Hour]++;

        foreach (var l in likes.Where(l => l.CreatedAt >= from))
            hourCounts[l.CreatedAt.Hour]++;

        return Enumerable.Range(0, 24)
            .Select(h => new PeakHour { Hora = h, Count = hourCounts[h] })
            .ToList();
    }

    /// <summary>
    /// Tasa de participación semanal por grupo de diagnóstico.
    /// Para cada diagnóstico, cuenta cuántos perfiles activos con ese diagnóstico
    /// tuvieron al menos una actividad en los últimos 7 días / total del grupo.
    /// </summary>
    private static IReadOnlyList<DiagnosisParticipation> BuildDiagnosisParticipation(
        IReadOnlyCollection<Profile> activeProfiles,
        IReadOnlyCollection<Post> posts,
        IReadOnlyCollection<Comment> comments,
        IReadOnlyCollection<Like> likes,
        IReadOnlyCollection<FeedEvent> feedEvents,
        DateTime utcNow)
    {
        var window7d = utcNow.AddDays(-7);
        var weeklyActiveIds = ComputeActiveProfileIdsInWindow(
            activeProfiles, posts, comments, likes, feedEvents, window7d, utcNow);

        // Agrupar perfiles activos por diagnóstico (no nulo)
        var groups = activeProfiles
            .Where(p => p.Diagnosis.HasValue)
            .GroupBy(p => p.Diagnosis!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Total = g.Count(),
                    Active = g.Count(p => weeklyActiveIds.Contains(p.Id)),
                });

        return groups
            .OrderBy(g => g.Key)
            .Select(g => new DiagnosisParticipation
            {
                Diagnosis = g.Key,
                Participation = g.Value.Total > 0
                    ? Math.Round(g.Value.Active * 100.0 / g.Value.Total, 1)
                    : 0.0,
            })
            .ToList();
    }
}
