using CoppAddresd.Community.GraphQL.Types;

namespace CoppAddresd.Community.Entities;

/// <summary>
/// Agregador puro y determinista para analytics de la comunidad.
/// Calcula regionStats, diagnosticStats, communityAnalytics a partir de
/// colecciones en memoria (sin EF/DbContext) para facilitar pruebas unitarias.
/// </summary>
public static class AnalyticsAggregator
{
    // ─── BUCKETS ────────────────────────────────────────────────────────

    private static readonly string[] StreakBucketRanges =
        ["1-6", "7-10", "11-21", "22-49", "50-89", "90+"];

    private static readonly string[] InactivityBucketRanges =
        ["1-3", "4-6", "7-9", "10-14", "15+"];

    /// <summary>Clasifica un valor de racha en el bucket correspondiente.</summary>
    public static string ClassifyStreakBucket(int streak)
    {
        if (streak <= 0) return "1-6";
        if (streak <= 6) return "1-6";
        if (streak <= 10) return "7-10";
        if (streak <= 21) return "11-21";
        if (streak <= 49) return "22-49";
        if (streak <= 89) return "50-89";
        return "90+";
    }

    /// <summary>Clasifica días de inactividad en el bucket correspondiente.</summary>
    public static string ClassifyInactivityBucket(int daysSinceActivity)
    {
        if (daysSinceActivity <= 3) return "1-3";
        if (daysSinceActivity <= 6) return "4-6";
        if (daysSinceActivity <= 9) return "7-9";
        if (daysSinceActivity <= 14) return "10-14";
        return "15+";
    }

    /// <summary>Clasifica el monto de XP según la razón: rachas, posts, o ERP.</summary>
    public static string ClassifyXpReason(string? reason)
    {
        if (string.IsNullOrEmpty(reason)) return "erp";
        var lower = reason.ToLowerInvariant();
        if (lower.Contains("racha")) return "rachas";
        if (lower.Contains("post")) return "posts";
        return "erp";
    }

    // ─── HELPERS ────────────────────────────────────────────────────────

    /// <summary>
    /// Determina si un perfil estaba inactivo antes del umbral dado:
    /// max(LastPostAt, LastActiveAt) es null o anterior al umbral.
    /// </summary>
    private static bool IsInactiveBefore(Profile profile, DateTime threshold)
    {
        var last = profile.LastPostAt.HasValue && profile.LastActiveAt.HasValue
            ? (profile.LastPostAt.Value > profile.LastActiveAt.Value ? profile.LastPostAt.Value : profile.LastActiveAt.Value)
            : profile.LastPostAt ?? profile.LastActiveAt;

        if (last is null) return true;
        return last.Value.UtcDateTime < threshold;
    }

    /// <summary>Calcula días desde la última actividad del perfil.</summary>
    private static int DaysSinceActivity(Profile profile, DateTime utcNow)
    {
        var last = profile.LastPostAt.HasValue && profile.LastActiveAt.HasValue
            ? (profile.LastPostAt.Value > profile.LastActiveAt.Value ? profile.LastPostAt.Value : profile.LastActiveAt.Value)
            : profile.LastPostAt ?? profile.LastActiveAt;

        if (last is null) return 15; // Sin actividad ⇒ bucket "15+"
        return (int)(utcNow - last.Value.UtcDateTime).TotalDays;
    }

    // ─── REGION STATS ──────────────────────────────────────────────────

    /// <summary>
    /// Agrupa perfiles activos no-sistema por región y calcula postsPerWeek.
    /// </summary>
    public static IReadOnlyList<RegionStat> ComputeRegionStats(
        IReadOnlyCollection<Profile> profiles,
        IReadOnlyCollection<Post> posts,
        DateTime utcNow)
    {
        var threshold30d = utcNow.AddDays(-30);
        var activeNonSystem = profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem && p.Region.HasValue)
            .ToList();

        var validProfileIds = new HashSet<Guid>(activeNonSystem.Select(p => p.Id));

        // Posts de perfiles activos no-sistema de los últimos 30 días (no eliminados).
        var recentPosts = posts
            .Where(p => p.DeletedAt == null && p.CreatedAt >= threshold30d && validProfileIds.Contains(p.ProfileId))
            .ToList();

        return activeNonSystem
            .GroupBy(p => p.Region!.Value)
            .Select(g =>
            {
                var regionIds = new HashSet<Guid>(g.Select(p => p.Id));
                var regionPosts = recentPosts.Count(p => regionIds.Contains(p.ProfileId));
                return new RegionStat
                {
                    Region = g.Key,
                    Members = g.Count(),
                    PostsPerWeek = Math.Round(regionPosts / 4.0, 2),
                };
            })
            .OrderBy(r => r.Region)
            .ToList();
    }

    // ─── DIAGNOSTIC STATS ──────────────────────────────────────────────

    /// <summary>
    /// Agrupa perfiles activos no-sistema por diagnóstico y calcula métricas.
    /// adherence = 100 × (perfiles con actividad max(LastPostAt, LastActiveAt) dentro de 7 días) / total.
    /// </summary>
    public static IReadOnlyList<DiagnosticStat> ComputeDiagnosticStats(
        IReadOnlyCollection<Profile> profiles,
        IReadOnlyCollection<Post> posts,
        DateTime utcNow)
    {
        var threshold30d = utcNow.AddDays(-30);
        var threshold7d = utcNow.AddDays(-7);
        var activeNonSystem = profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem && p.Diagnosis.HasValue)
            .ToList();

        var validProfileIds = new HashSet<Guid>(activeNonSystem.Select(p => p.Id));

        var recentPosts = posts
            .Where(p => p.DeletedAt == null && p.CreatedAt >= threshold30d && validProfileIds.Contains(p.ProfileId))
            .ToList();

        return activeNonSystem
            .GroupBy(p => p.Diagnosis!.Value)
            .Select(g =>
            {
                var diagIds = new HashSet<Guid>(g.Select(p => p.Id));
                var diagPosts = recentPosts.Count(p => diagIds.Contains(p.ProfileId));
                var members = g.Count();

                // adherence: perfiles con actividad en últimos 7 días
                var recentMembers = g.Count(p =>
                {
                    var last = p.LastPostAt.HasValue && p.LastActiveAt.HasValue
                        ? (p.LastPostAt.Value > p.LastActiveAt.Value ? p.LastPostAt.Value : p.LastActiveAt.Value)
                        : p.LastPostAt ?? p.LastActiveAt;
                    return last is not null && last.Value.UtcDateTime >= threshold7d;
                });

                var adherence = members > 0
                    ? Math.Round(recentMembers * 100.0 / members, 1)
                    : 0.0;

                var avgStreak = g.Average(p => (double)p.CurrentStreak);
                var avgXp = g.Average(p => (double)p.XpTotal);

                return new DiagnosticStat
                {
                    Diagnosis = g.Key,
                    Members = members,
                    PostsPerWeek = Math.Round(diagPosts / 4.0, 2),
                    AvgStreak = Math.Round(avgStreak, 1),
                    AvgXp = Math.Round(avgXp, 0),
                    Adherence = adherence,
                };
            })
            .OrderBy(d => d.Diagnosis)
            .ToList();
    }

    // ─── FEED TODAY ────────────────────────────────────────────────────

    /// <summary>Calcula métricas de las últimas 24 horas.</summary>
    public static FeedToday ComputeFeedToday(
        IReadOnlyCollection<Profile> profiles,
        IReadOnlyCollection<Post> posts,
        IReadOnlyCollection<Comment> comments,
        IReadOnlyCollection<Like> likes,
        IReadOnlyCollection<XpEntry> xpEntries,
        DateTime utcNow)
    {
        var window24h = utcNow.AddHours(-24);
        var activeNonSystem = profiles.Where(p => p.Status == ProfileStatus.Active && !p.IsSystem).ToList();
        var activeIds = new HashSet<Guid>(activeNonSystem.Select(p => p.Id));

        var windowPosts = posts.Count(p => p.DeletedAt == null && p.CreatedAt >= window24h && activeIds.Contains(p.ProfileId));
        var windowComments = comments.Count(c => c.DeletedAt == null && c.CreatedAt >= window24h && activeIds.Contains(c.ProfileId));
        var windowLikes = likes.Count(l => l.CreatedAt >= window24h && activeIds.Contains(l.ProfileId));

        // Perfiles creados en la ventana (newMembers)
        var windowNewMembers = profiles.Count(p => p.CreatedAt >= window24h && !p.IsSystem);

        // XP total otorgado en la ventana
        var xpDelivered = xpEntries
            .Where(x => x.CreatedAt >= window24h && activeIds.Contains(x.ProfileId))
            .Sum(x => x.Amount);

        // Rachas rotas: perfiles activos no-sistema con CurrentStreak == 0 && BestStreak > 0
        var streaksBroken = activeNonSystem.Count(p => p.CurrentStreak == 0 && p.BestStreak > 0);

        return new FeedToday
        {
            Posts = windowPosts,
            Comments = windowComments,
            Reactions = windowLikes,
            NewMembers = windowNewMembers,
            StreaksBroken = streaksBroken,
            XpDelivered = xpDelivered,
        };
    }

    // ─── STREAK OVERVIEW ───────────────────────────────────────────────

    /// <summary>Calcula el resumen general de rachas.</summary>
    public static StreakOverview ComputeStreakOverview(
        IReadOnlyCollection<Profile> profiles,
        IReadOnlyCollection<FeedEvent> feedEvents,
        DateTime utcNow)
    {
        var activeNonSystem = profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
            .ToList();

        if (activeNonSystem.Count == 0)
        {
            return new StreakOverview
            {
                Distribution = StreakBucketRanges.Select(r => new RangeBucket { Range = r, Value = 0 }).ToList(),
            };
        }

        // Longest streak
        var longest = activeNonSystem.OrderByDescending(p => p.CurrentStreak).First();

        // Members over 7 days
        var membersOverSeven = activeNonSystem.Count(p => p.CurrentStreak >= 7);

        // Milestones this month (FeedEvents kind Hito|Racha in current calendar month)
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var milestonesThisMonth = feedEvents.Count(f =>
            (f.Kind == FeedEventKind.Hito || f.Kind == FeedEventKind.Racha) &&
            f.CreatedAt >= monthStart);

        // Streaks broken: CurrentStreak == 0 && BestStreak > 0
        var streaksBroken = activeNonSystem.Count(p => p.CurrentStreak == 0 && p.BestStreak > 0);

        // Distribution: buckets by CurrentStreak > 0
        var streakDistribution = activeNonSystem
            .Where(p => p.CurrentStreak > 0)
            .GroupBy(p => ClassifyStreakBucket(p.CurrentStreak))
            .ToDictionary(g => g.Key, g => g.Count());

        var distribution = StreakBucketRanges
            .Select(r => new RangeBucket
            {
                Range = r,
                Value = streakDistribution.GetValueOrDefault(r, 0),
            })
            .ToList();

        return new StreakOverview
        {
            LongestStreak = longest.CurrentStreak,
            LongestProfileId = longest.Id,
            LongestProfileName = longest.DisplayName,
            MembersOverSevenDays = membersOverSeven,
            MilestonesThisMonth = milestonesThisMonth,
            StreaksBroken = streaksBroken,
            Distribution = distribution,
        };
    }

    // ─── INACTIVITY DISTRIBUTION ───────────────────────────────────────

    /// <summary>
    /// Calcula la distribución de días desde la última actividad para
    /// perfiles activos no-sistema.
    /// </summary>
    public static IReadOnlyList<RangeBucket> ComputeInactivityDistribution(
        IReadOnlyCollection<Profile> profiles,
        DateTime utcNow)
    {
        var activeNonSystem = profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
            .ToList();

        var buckets = new Dictionary<string, int>();
        foreach (var range in InactivityBucketRanges)
            buckets[range] = 0;

        foreach (var profile in activeNonSystem)
        {
            var days = DaysSinceActivity(profile, utcNow);
            var bucket = ClassifyInactivityBucket(days);
            buckets[bucket]++;
        }

        return InactivityBucketRanges
            .Select(r => new RangeBucket { Range = r, Value = buckets[r] })
            .ToList();
    }

    // ─── XP DELIVERED SERIES ──────────────────────────────────────────

    /// <summary>
    /// Calcula la serie semanal de XP de las 4 últimas semanas (más antigua primero).
    /// Clasifica por razón: rachas, posts, o ERP.
    /// </summary>
    public static IReadOnlyList<XpWeekPoint> ComputeXpDeliveredSeries(
        IReadOnlyCollection<XpEntry> xpEntries,
        DateTime utcNow)
    {
        // Encontrar el inicio de la semana ISO actual
        var today = utcNow.Date;
        var dayOfWeek = today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek;
        var startOfWeek = today.AddDays(-(dayOfWeek - 1)); // Lunes de esta semana

        // 4 semanas: anterior-3, anterior-2, anterior-1, actual
        var weeks = new (string Label, DateTime Start, DateTime End)[4];
        for (var i = 0; i < 4; i++)
        {
            var weekStart = startOfWeek.AddDays(-7 * (3 - i));
            var weekEnd = weekStart.AddDays(7);
            // Etiqueta "d/M" estilo español
            weeks[i] = ($"{weekStart.Day}/{weekStart.Month}", weekStart, weekEnd);
        }

        return weeks.Select(w =>
        {
            var weekEntries = xpEntries.Where(x => x.CreatedAt >= w.Start && x.CreatedAt < w.End).ToList();
            var rachas = 0;
            var posts = 0;
            var erp = 0;

            foreach (var entry in weekEntries)
            {
                switch (ClassifyXpReason(entry.Reason))
                {
                    case "rachas": rachas += entry.Amount; break;
                    case "posts": posts += entry.Amount; break;
                    default: erp += entry.Amount; break;
                }
            }

            return new XpWeekPoint
            {
                Label = w.Label,
                Rachas = rachas,
                Posts = posts,
                Erp = erp,
            };
        }).ToList();
    }

    // ─── MESSAGE REACH ─────────────────────────────────────────────────

    /// <summary>
    /// Calcula el alcance de mensajes del sistema por alcance:
    /// TODOS, INACTIVOS, ACTIVOS7.
    /// </summary>
    public static IReadOnlyList<MessageReach> ComputeMessageReach(
        IReadOnlyCollection<Profile> profiles,
        IReadOnlyCollection<Message> messages,
        DateTime utcNow)
    {
        var threshold7d = utcNow.AddDays(-7);
        var activeNonSystem = profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
            .ToList();

        // Mensajes enviados desde sistema con TriggeredByProfileId != null
        var systemBulkMessages = messages
            .Where(m => m.TriggeredByProfileId != null && m.RecipientProfileId != null)
            .ToList();

        var totalActive = activeNonSystem.Count;

        // INACTIVOS: mismo criterio que SendBulkMessage
        var inactiveProfiles = activeNonSystem.Where(p =>
        {
            var last = p.LastPostAt.HasValue && p.LastActiveAt.HasValue
                ? (p.LastPostAt.Value > p.LastActiveAt.Value ? p.LastPostAt.Value : p.LastActiveAt.Value)
                : p.LastPostAt ?? p.LastActiveAt;
            return last is null || last.Value.UtcDateTime < threshold7d;
        }).ToList();

        // ACTIVOS7: perfiles activos con actividad dentro de 7 días
        var active7Profiles = activeNonSystem.Where(p =>
        {
            var last = p.LastPostAt.HasValue && p.LastActiveAt.HasValue
                ? (p.LastPostAt.Value > p.LastActiveAt.Value ? p.LastPostAt.Value : p.LastActiveAt.Value)
                : p.LastPostAt ?? p.LastActiveAt;
            return last is not null && last.Value.UtcDateTime >= threshold7d;
        }).ToList();

        // reached per scope: destinatarios de mensajes del sistema que pertenecen al alcance
        var totalRecipients = systemBulkMessages
            .Where(m => activeNonSystem.Any(p => p.Id == m.RecipientProfileId!.Value))
            .Select(m => m.RecipientProfileId!.Value)
            .Distinct()
            .Count();

        var inactiveRecipients = systemBulkMessages
            .Where(m => inactiveProfiles.Any(p => p.Id == m.RecipientProfileId!.Value))
            .Select(m => m.RecipientProfileId!.Value)
            .Distinct()
            .Count();

        var active7Recipients = systemBulkMessages
            .Where(m => active7Profiles.Any(p => p.Id == m.RecipientProfileId!.Value))
            .Select(m => m.RecipientProfileId!.Value)
            .Distinct()
            .Count();

        return
        [
            new MessageReach { Scope = "TODOS", Total = totalActive, Reached = totalRecipients },
            new MessageReach { Scope = "INACTIVOS", Total = inactiveProfiles.Count, Reached = inactiveRecipients },
            new MessageReach { Scope = "ACTIVOS7", Total = active7Profiles.Count, Reached = active7Recipients },
        ];
    }
}
