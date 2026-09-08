using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Types;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Tests unitarios del agregador del dashboard de la comunidad (DashboardAggregator.Compute).
/// Usa colecciones en memoria sin base de datos.
/// </summary>
public sealed class DashboardAggregatorTests
{
    private static readonly DateTime BaseNow = new(2026, 8, 27, 14, 0, 0, DateTimeKind.Utc);

    // ─── Helpers ────────────────────────────────────────────────────────

    private static Profile MakeProfile(
        Guid? id = null,
        ProfileStatus status = ProfileStatus.Active,
        DateTimeOffset? lastPostAt = null,
        DateTimeOffset? lastActiveAt = null,
        ProfileDiagnosis? diagnosis = ProfileDiagnosis.DM2)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Test User",
            Status = status,
            Diagnosis = diagnosis,
            LastPostAt = lastPostAt,
            LastActiveAt = lastActiveAt,
            CreatedAt = BaseNow.AddDays(-60),
        };

    private static Post MakePost(
        Guid profileId,
        DateTime createdAt,
        PostType type = PostType.Texto,
        DateTime? deletedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            Body = "Test post",
            Type = type,
            CreatedAt = createdAt,
            DeletedAt = deletedAt,
        };

    private static Comment MakeComment(
        Guid profileId, Guid postId, DateTime createdAt, DateTime? deletedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            PostId = postId,
            Body = "Test comment",
            CreatedAt = createdAt,
            DeletedAt = deletedAt,
        };

    private static Like MakeLike(Guid profileId, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            PostId = Guid.NewGuid(),
            CreatedAt = createdAt,
        };

    private static Repost MakeRepost(Guid profileId, Guid postId, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            PostId = postId,
            CreatedAt = createdAt,
        };

    // ─── Datos vacíos → zeros ──────────────────────────────────────────

    [Fact]
    public void Compute_EmptyData_ReturnsZeros()
    {
        var result = DashboardAggregator.Compute([], [], [], [], [], [], BaseNow);

        Assert.Equal(0, result.ActiveMembers);
        Assert.Equal(0, result.PostsThisMonth);
        Assert.Equal(0.0, result.ParticipationRate);
        Assert.Equal(0, result.InactiveOver7Days);
        Assert.Equal(0, result.InactiveAtRisk);
        Assert.Equal(30, result.ActivitySeries.Count);
        Assert.Equal(5, result.PostTypes.Count);
        Assert.Equal(24, result.PeakHours.Count);
        Assert.Empty(result.DiagnosisParticipation);
    }

    // ─── activeMembers ──────────────────────────────────────────────────

    [Fact]
    public void Compute_ActiveMembers_CountsOnlyActiveProfiles()
    {
        var active = MakeProfile(status: ProfileStatus.Active);
        var banned = MakeProfile(status: ProfileStatus.Banned);
        var profiles = new List<Profile> { active, banned };

        var result = DashboardAggregator.Compute(profiles, [], [], [], [], [], BaseNow);

        Assert.Equal(1, result.ActiveMembers);
    }

    // ─── postsThisMonth ─────────────────────────────────────────────────

    [Fact]
    public void Compute_PostsThisMonth_CountsCurrentMonth()
    {
        var profile = MakeProfile();
        var thisMonth = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastMonth = new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);

        var posts = new List<Post>
        {
            MakePost(profile.Id, thisMonth),
            MakePost(profile.Id, thisMonth),
            MakePost(profile.Id, lastMonth), // Mes anterior, no cuenta
        };

        var result = DashboardAggregator.Compute([profile], posts, [], [], [], [], BaseNow);

        Assert.Equal(2, result.PostsThisMonth);
    }

    [Fact]
    public void Compute_PostsThisMonth_ExcludesDeletedPosts()
    {
        var profile = MakeProfile();
        var thisMonth = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

        var posts = new List<Post>
        {
            MakePost(profile.Id, thisMonth),
            MakePost(profile.Id, thisMonth, deletedAt: thisMonth), // Eliminado
        };

        var result = DashboardAggregator.Compute([profile], posts, [], [], [], [], BaseNow);

        Assert.Equal(1, result.PostsThisMonth);
    }

    // ─── participationRate ──────────────────────────────────────────────

    [Fact]
    public void Compute_ParticipationRate_CalculatesCorrectly()
    {
        var now = BaseNow;
        // 3 perfiles activos
        var p1 = MakeProfile(lastActiveAt: now.AddDays(-1));  // activo esta semana
        var p2 = MakeProfile(lastActiveAt: now.AddDays(-3));  // activo esta semana
        var p3 = MakeProfile(lastActiveAt: now.AddDays(-20)); // inactivo

        // p1 y p2 tuvieron actividad reciente (un like basta)
        var likes = new List<Like>
        {
            MakeLike(p1.Id, now.AddDays(-2)),
            MakeLike(p2.Id, now.AddDays(-1)),
        };

        var result = DashboardAggregator.Compute([p1, p2, p3], [], [], likes, [], [], now);

        // 2 de 3 activos con actividad en 7d = 66.7%
        Assert.Equal(66.7, result.ParticipationRate, 1);
    }

    // ─── inactiveOver7Days ─────────────────────────────────────────────

    [Fact]
    public void Compute_InactiveOver7Days_CountsInactiveActiveProfiles()
    {
        var now = BaseNow;
        var active = MakeProfile(lastActiveAt: now.AddDays(-10)); // Inactivo >7d
        var recent = MakeProfile(lastActiveAt: now.AddDays(-2));  // Activo
        var banned = MakeProfile(
            status: ProfileStatus.Banned,
            lastActiveAt: now.AddDays(-10)); // Banned, no cuenta

        var result = DashboardAggregator.Compute([active, recent, banned], [], [], [], [], [], now);

        Assert.Equal(1, result.InactiveOver7Days);
    }

    [Fact]
    public void Compute_InactiveOver7Days_ProfileWithNullActivity_IsInactive()
    {
        var now = BaseNow;
        var noActivity = MakeProfile(lastPostAt: null, lastActiveAt: null);

        var result = DashboardAggregator.Compute([noActivity], [], [], [], [], [], now);

        Assert.Equal(1, result.InactiveOver7Days);
    }

    // ─── inactiveAtRisk ─────────────────────────────────────────────────

    [Fact]
    public void Compute_InactiveAtRisk_OnlyAltoRiskAmongInactive()
    {
        var now = BaseNow;
        // Alto: >14 días sin actividad
        var alto = MakeProfile(lastPostAt: now.AddDays(-20));
        // Medio: 7-14 días sin actividad
        var medio = MakeProfile(lastPostAt: now.AddDays(-10));

        var result = DashboardAggregator.Compute([alto, medio], [], [], [], [], [], now);

        Assert.Equal(1, result.InactiveAtRisk);
    }

    // ─── activitySeries ─────────────────────────────────────────────────

    [Fact]
    public void Compute_ActivitySeries_Has30Entries()
    {
        var result = DashboardAggregator.Compute([], [], [], [], [], [], BaseNow);

        Assert.Equal(30, result.ActivitySeries.Count);
        // Todos los días deben estar presentes con etiqueta "MMM d" (ej. "sep 4").
        foreach (var day in result.ActivitySeries)
        {
            Assert.Matches(@"^[A-Za-z]{3} \d{1,2}$", day.Dia);
        }
    }

    [Fact]
    public void Compute_ActivitySeries_SumsMatchTotals()
    {
        var profile = MakeProfile();
        var today = BaseNow.Date;
        var yesterday = today.AddDays(-1);

        var posts = new List<Post>
        {
            MakePost(profile.Id, today.AddHours(10)),
            MakePost(profile.Id, today.AddHours(14)),
            MakePost(profile.Id, yesterday.AddHours(8)),
        };
        var comments = new List<Comment>
        {
            MakeComment(profile.Id, Guid.NewGuid(), today.AddHours(12)),
        };
        var likes = new List<Like>
        {
            MakeLike(profile.Id, today.AddHours(16)),
        };

        var result = DashboardAggregator.Compute([profile], posts, comments, likes, [], [], BaseNow);

        var totalPosts = result.ActivitySeries.Sum(d => d.Posts);
        var totalComments = result.ActivitySeries.Sum(d => d.Comentarios);
        var totalLikes = result.ActivitySeries.Sum(d => d.Reacciones);

        Assert.Equal(3, totalPosts);
        Assert.Equal(1, totalComments);
        Assert.Equal(1, totalLikes);
    }

    [Fact]
    public void Compute_ActivitySeries_RepostsCountedInReacciones()
    {
        var profile = MakeProfile();
        var today = BaseNow.Date;
        var postId = Guid.NewGuid();

        var reposts = new List<Repost>
        {
            MakeRepost(profile.Id, postId, today.AddHours(11)),
            MakeRepost(profile.Id, postId, today.AddHours(15)),
        };
        var likes = new List<Like>
        {
            MakeLike(profile.Id, today.AddHours(16)),
        };

        var result = DashboardAggregator.Compute([profile], [], [], likes, reposts, [], BaseNow);

        var totalReacciones = result.ActivitySeries.Sum(d => d.Reacciones);
        // 1 like + 2 reposts = 3 reacciones
        Assert.Equal(3, totalReacciones);
    }

    // ─── postTypes ──────────────────────────────────────────────────────

    [Fact]
    public void Compute_PostTypes_Has5Entries()
    {
        var result = DashboardAggregator.Compute([], [], [], [], [], [], BaseNow);

        Assert.Equal(5, result.PostTypes.Count);
        // Cada tipo del enum debe aparecer exactamente una vez
        var types = result.PostTypes.Select(pt => pt.Type).OrderBy(t => t).ToList();
        var allTypes = Enum.GetValues<PostType>().OrderBy(t => t).ToList();
        Assert.Equal(allTypes, types);
    }

    [Fact]
    public void Compute_PostTypes_CountsPerType()
    {
        var profile = MakeProfile();
        var thisMonth = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

        var posts = new List<Post>
        {
            MakePost(profile.Id, thisMonth, PostType.Texto),
            MakePost(profile.Id, thisMonth, PostType.Texto),
            MakePost(profile.Id, thisMonth, PostType.Imagen),
            MakePost(profile.Id, thisMonth, PostType.Video),
        };

        var result = DashboardAggregator.Compute([profile], posts, [], [], [], [], BaseNow);

        Assert.Equal(2, result.PostTypes.First(pt => pt.Type == PostType.Texto).Count);
        Assert.Equal(1, result.PostTypes.First(pt => pt.Type == PostType.Imagen).Count);
        Assert.Equal(1, result.PostTypes.First(pt => pt.Type == PostType.Video).Count);
        Assert.Equal(0, result.PostTypes.First(pt => pt.Type == PostType.Logro).Count);
    }

    // ─── peakHours ──────────────────────────────────────────────────────

    [Fact]
    public void Compute_PeakHours_Has24Entries()
    {
        var result = DashboardAggregator.Compute([], [], [], [], [], [], BaseNow);

        Assert.Equal(24, result.PeakHours.Count);
        var horas = result.PeakHours.Select(p => p.Hora).OrderBy(h => h).ToList();
        Assert.Equal(Enumerable.Range(0, 24).ToList(), horas);
    }

    [Fact]
    public void Compute_PeakHours_CountsActivityByHour()
    {
        var profile = MakeProfile();
        // Fechas dentro de la ventana de 30 días con horas explícitas seguras
        var posts = new List<Post>
        {
            MakePost(profile.Id, new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc)),
            MakePost(profile.Id, new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc)),
            MakePost(profile.Id, new DateTime(2026, 8, 20, 14, 0, 0, DateTimeKind.Utc)),
        };

        var result = DashboardAggregator.Compute([profile], posts, [], [], [], [], BaseNow);

        var hour10 = result.PeakHours.First(p => p.Hora == 10).Count;
        var hour14 = result.PeakHours.First(p => p.Hora == 14).Count;
        var hour0 = result.PeakHours.First(p => p.Hora == 0).Count;

        Assert.Equal(2, hour10);
        Assert.Equal(1, hour14);
        Assert.Equal(0, hour0);
    }

    // ─── diagnosisParticipation ─────────────────────────────────────────

    [Fact]
    public void Compute_DiagnosisParticipation_GroupsByDiagnosis()
    {
        var now = BaseNow;
        var p1 = MakeProfile(diagnosis: ProfileDiagnosis.DM2, lastActiveAt: now.AddDays(-1));
        var p2 = MakeProfile(diagnosis: ProfileDiagnosis.DM2, lastActiveAt: now.AddDays(-20)); // inactivo
        var p3 = MakeProfile(diagnosis: ProfileDiagnosis.Obesidad, lastActiveAt: now.AddDays(-1));
        var p4 = MakeProfile(diagnosis: ProfileDiagnosis.Prediabetes, lastActiveAt: now.AddDays(-30)); // inactivo

        // p1 y p3 tuvieron actividad esta semana (necesitan registros reales, no solo LastActiveAt)
        var likes = new List<Like>
        {
            MakeLike(p1.Id, now.AddDays(-2)),
            MakeLike(p3.Id, now.AddDays(-1)),
        };

        var result = DashboardAggregator.Compute([p1, p2, p3, p4], [], [], likes, [], [], now);

        Assert.Equal(3, result.DiagnosisParticipation.Count);

        var dm2 = result.DiagnosisParticipation.First(d => d.Diagnosis == ProfileDiagnosis.DM2);
        // 1 de 2 activos con actividad = 50%
        Assert.Equal(50.0, dm2.Participation, 1);

        var obs = result.DiagnosisParticipation.First(d => d.Diagnosis == ProfileDiagnosis.Obesidad);
        // 1 de 1 activo con actividad = 100%
        Assert.Equal(100.0, obs.Participation, 1);

        var pre = result.DiagnosisParticipation.First(d => d.Diagnosis == ProfileDiagnosis.Prediabetes);
        // 0 de 1 activo con actividad = 0%
        Assert.Equal(0.0, pre.Participation, 1);
    }

    [Fact]
    public void Compute_DiagnosisParticipation_ExcludesNullDiagnosis()
    {
        var now = BaseNow;
        var withDiag = MakeProfile(diagnosis: ProfileDiagnosis.DM2, lastActiveAt: now.AddDays(-1));
        var noDiag = MakeProfile(diagnosis: null, lastActiveAt: now.AddDays(-1));

        var result = DashboardAggregator.Compute([withDiag, noDiag], [], [], [], [], [], now);

        // Solo un grupo (DM2), null se excluye
        Assert.Single(result.DiagnosisParticipation);
        Assert.Equal(ProfileDiagnosis.DM2, result.DiagnosisParticipation[0].Diagnosis);
    }

    // ─── kpiTrends ──────────────────────────────────────────────────────

    [Fact]
    public void Compute_KpiTrends_PostsThisMonthDelta()
    {
        var now = BaseNow;
        var currentMonthStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var profile = MakeProfile();

        var posts = new List<Post>
        {
            MakePost(profile.Id, currentMonthStart.AddDays(5)),  // Este mes: 1
            MakePost(profile.Id, currentMonthStart.AddDays(10)),
            MakePost(profile.Id, prevMonthStart.AddDays(5)),    // Mes anterior: 1
        };

        var result = DashboardAggregator.Compute([profile], posts, [], [], [], [], now);

        // (2 - 1) / 1 * 100 = 100%
        Assert.Equal(100.0, result.KpiTrends.PostsThisMonth, 1);
    }

    [Fact]
    public void Compute_KpiTrends_InactiveOver7DaysDelta()
    {
        var now = BaseNow;
        // Inactivo ahora: LastActiveAt hace 10 días (< 7d → inactivo >7d)
        var p1 = MakeProfile(lastActiveAt: now.AddDays(-10));
        // Inactivo hace 30 días: LastActiveAt hace 40 días (antes de now-37)
        var p2 = MakeProfile(lastActiveAt: now.AddDays(-40));

        var result = DashboardAggregator.Compute([p1, p2], [], [], [], [], [], now);

        // inactiveNow=2, inactive30dAgo=1 → (2-1)/1*100 = 100%
        Assert.Equal(100.0, result.KpiTrends.InactiveOver7Days, 1);
    }

    // ─── KPI trends con denominador 0 → 0 ──────────────────────────────

    [Fact]
    public void Compute_KpiTrends_ZeroDenominator_ReturnsZero()
    {
        var now = BaseNow;
        // Todos los perfiles recién creados sin actividad → denominator 0 para activeMembers trend
        var profiles = new List<Profile>
        {
            MakeProfile(lastActiveAt: now.AddDays(-5)),
        };

        var result = DashboardAggregator.Compute(profiles, [], [], [], [], [], now);

        // activeInPrior30d = 0 → delta = 0
        Assert.Equal(0.0, result.KpiTrends.ActiveMembers, 1);
    }
}
