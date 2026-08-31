using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Types;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Tests unitarios del agregador de analytics de la comunidad (AnalyticsAggregator).
/// Usa colecciones en memoria sin base de datos.
/// </summary>
public sealed class AnalyticsAggregatorTests
{
    private static readonly DateTime BaseNow = new(2026, 8, 27, 14, 0, 0, DateTimeKind.Utc);

    // ─── Helpers ────────────────────────────────────────────────────────

    private static Profile MakeProfile(
        Guid? id = null,
        ProfileStatus status = ProfileStatus.Active,
        ProfileRegion? region = ProfileRegion.Miami,
        ProfileDiagnosis? diagnosis = ProfileDiagnosis.DM2,
        int currentStreak = 0,
        int bestStreak = 0,
        int xpTotal = 0,
        DateTimeOffset? lastPostAt = null,
        DateTimeOffset? lastActiveAt = null,
        bool isSystem = false)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            UserId = isSystem ? null : Guid.NewGuid(),
            IsSystem = isSystem,
            DisplayName = "Test User",
            Status = status,
            Region = region,
            Diagnosis = diagnosis,
            CurrentStreak = currentStreak,
            BestStreak = bestStreak,
            XpTotal = xpTotal,
            LastPostAt = lastPostAt,
            LastActiveAt = lastActiveAt,
            CreatedAt = BaseNow.AddDays(-60),
        };

    private static Post MakePost(Guid profileId, DateTime createdAt, DateTime? deletedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            Body = "Test post",
            CreatedAt = createdAt,
            DeletedAt = deletedAt,
        };

    private static Comment MakeComment(Guid profileId, DateTime createdAt, DateTime? deletedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            PostId = Guid.NewGuid(),
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

    private static XpEntry MakeXpEntry(Guid profileId, int amount, string? reason = null, DateTime? createdAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            Amount = amount,
            Reason = reason,
            CreatedAt = createdAt ?? BaseNow,
        };

    private static FeedEvent MakeFeedEvent(FeedEventKind kind, DateTime createdAt, Guid? profileId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProfileId = profileId,
            Kind = kind,
            Body = "Test event",
            CreatedAt = createdAt,
        };

    private static Message MakeMessage(Guid senderId, Guid recipientId, DateTime createdAt, Guid? triggeredBy = null)
        => new()
        {
            Id = Guid.NewGuid(),
            SenderProfileId = senderId,
            RecipientProfileId = recipientId,
            Body = "Test message",
            CreatedAt = createdAt,
            TriggeredByProfileId = triggeredBy,
        };

    // ─── ClassifyStreakBucket ───────────────────────────────────────────

    [Theory]
    [InlineData(0, "1-6")]
    [InlineData(1, "1-6")]
    [InlineData(6, "1-6")]
    [InlineData(7, "7-10")]
    [InlineData(10, "7-10")]
    [InlineData(11, "11-21")]
    [InlineData(21, "11-21")]
    [InlineData(22, "22-49")]
    [InlineData(49, "22-49")]
    [InlineData(50, "50-89")]
    [InlineData(89, "50-89")]
    [InlineData(90, "90+")]
    [InlineData(365, "90+")]
    public void ClassifyStreakBucket_ReturnsCorrectRange(int streak, string expected)
        => Assert.Equal(expected, AnalyticsAggregator.ClassifyStreakBucket(streak));

    // ─── ClassifyInactivityBucket ───────────────────────────────────────

    [Theory]
    [InlineData(0, "1-3")]
    [InlineData(3, "1-3")]
    [InlineData(4, "4-6")]
    [InlineData(6, "4-6")]
    [InlineData(7, "7-9")]
    [InlineData(9, "7-9")]
    [InlineData(10, "10-14")]
    [InlineData(14, "10-14")]
    [InlineData(15, "15+")]
    [InlineData(30, "15+")]
    public void ClassifyInactivityBucket_ReturnsCorrectRange(int days, string expected)
        => Assert.Equal(expected, AnalyticsAggregator.ClassifyInactivityBucket(days));

    // ─── ClassifyXpReason ──────────────────────────────────────────────

    [Theory]
    [InlineData(null, "erp")]
    [InlineData("", "erp")]
    [InlineData("Racha destacada", "rachas")]
    [InlineData("RACHA DIARIA", "rachas")]
    [InlineData("Compartir post", "posts")]
    [InlineData("Post compartido", "posts")]
    [InlineData("Puntos XP", "erp")]
    [InlineData("Miembro del mes", "erp")]
    public void ClassifyXpReason_ReturnsCorrectCategory(string? reason, string expected)
        => Assert.Equal(expected, AnalyticsAggregator.ClassifyXpReason(reason));

    // ─── ComputeRegionStats ────────────────────────────────────────────

    [Fact]
    public void ComputeRegionStats_EmptyData_ReturnsEmpty()
    {
        var result = AnalyticsAggregator.ComputeRegionStats([], [], BaseNow);
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeRegionStats_GroupsByRegion_CalculatesPostsPerWeek()
    {
        var p1 = MakeProfile(region: ProfileRegion.Miami);
        var p2 = MakeProfile(region: ProfileRegion.Miami);
        var p3 = MakeProfile(region: ProfileRegion.NY);

        var posts = new List<Post>
        {
            MakePost(p1.Id, BaseNow.AddDays(-5)),
            MakePost(p1.Id, BaseNow.AddDays(-10)),
            MakePost(p2.Id, BaseNow.AddDays(-15)),
            MakePost(p3.Id, BaseNow.AddDays(-3)), // NY: 1 post in 30d
        };

        var result = AnalyticsAggregator.ComputeRegionStats([p1, p2, p3], posts, BaseNow);

        Assert.Equal(2, result.Count);
        var miami = result.First(r => r.Region == ProfileRegion.Miami);
        Assert.Equal(2, miami.Members);
        // 3 posts from Miami profiles in 30d → 3/4 = 0.75
        Assert.Equal(0.75, miami.PostsPerWeek, 2);

        var ny = result.First(r => r.Region == ProfileRegion.NY);
        Assert.Equal(1, ny.Members);
        Assert.Equal(0.25, ny.PostsPerWeek, 2);
    }

    [Fact]
    public void ComputeRegionStats_ExcludesSystemProfiles()
    {
        var real = MakeProfile(region: ProfileRegion.Miami);
        var system = MakeProfile(region: ProfileRegion.Miami, isSystem: true);

        var result = AnalyticsAggregator.ComputeRegionStats([real, system], [], BaseNow);

        var miami = result.First(r => r.Region == ProfileRegion.Miami);
        Assert.Equal(1, miami.Members);
    }

    // ─── ComputeDiagnosticStats ─────────────────────────────────────────

    [Fact]
    public void ComputeDiagnosticStats_EmptyData_ReturnsEmpty()
    {
        var result = AnalyticsAggregator.ComputeDiagnosticStats([], [], BaseNow);
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeDiagnosticStats_CalculatesAvgStreakAndXp()
    {
        var p1 = MakeProfile(diagnosis: ProfileDiagnosis.DM2, currentStreak: 10, xpTotal: 1000);
        var p2 = MakeProfile(diagnosis: ProfileDiagnosis.DM2, currentStreak: 20, xpTotal: 2000);
        var p3 = MakeProfile(diagnosis: ProfileDiagnosis.Obesidad, currentStreak: 5, xpTotal: 500);

        var result = AnalyticsAggregator.ComputeDiagnosticStats([p1, p2, p3], [], BaseNow);

        var dm2 = result.First(d => d.Diagnosis == ProfileDiagnosis.DM2);
        Assert.Equal(2, dm2.Members);
        Assert.Equal(15.0, dm2.AvgStreak, 1); // (10+20)/2
        Assert.Equal(1500.0, dm2.AvgXp, 0); // (1000+2000)/2

        var obs = result.First(d => d.Diagnosis == ProfileDiagnosis.Obesidad);
        Assert.Equal(1, obs.Members);
        Assert.Equal(5.0, obs.AvgStreak, 1);
    }

    [Fact]
    public void ComputeDiagnosticStats_Adherence_ActiveWithin7Days()
    {
        var now = BaseNow;
        // Active within 7d
        var recent = MakeProfile(diagnosis: ProfileDiagnosis.DM2, lastPostAt: now.AddDays(-3));
        // Inactive >7d
        var inactive = MakeProfile(diagnosis: ProfileDiagnosis.DM2, lastPostAt: now.AddDays(-10));

        var result = AnalyticsAggregator.ComputeDiagnosticStats([recent, inactive], [], now);

        var dm2 = result.First(d => d.Diagnosis == ProfileDiagnosis.DM2);
        Assert.Equal(2, dm2.Members);
        // 1 of 2 active within 7d → 50%
        Assert.Equal(50.0, dm2.Adherence, 1);
    }

    // ─── ComputeFeedToday ──────────────────────────────────────────────

    [Fact]
    public void ComputeFeedToday_EmptyData_ReturnsZeros()
    {
        var result = AnalyticsAggregator.ComputeFeedToday([], [], [], [], [], BaseNow);
        Assert.Equal(0, result.Posts);
        Assert.Equal(0, result.Comments);
        Assert.Equal(0, result.Reactions);
        Assert.Equal(0, result.NewMembers);
        Assert.Equal(0, result.XpDelivered);
    }

    [Fact]
    public void ComputeFeedToday_CountsWindow24h()
    {
        var now = BaseNow;
        var p1 = MakeProfile();

        var posts = new List<Post>
        {
            MakePost(p1.Id, now.AddHours(-2)),  // within 24h
            MakePost(p1.Id, now.AddHours(-30)), // outside 24h
        };
        var xp = new List<XpEntry>
        {
            MakeXpEntry(p1.Id, 100, createdAt: now.AddHours(-5)),
            MakeXpEntry(p1.Id, 50, createdAt: now.AddHours(-30)),
        };

        var result = AnalyticsAggregator.ComputeFeedToday([p1], posts, [], [], xp, now);

        Assert.Equal(1, result.Posts);
        Assert.Equal(100, result.XpDelivered);
    }

    [Fact]
    public void ComputeFeedToday_StreaksBroken_CountsActiveProfilesWithZeroStreak()
    {
        var broken = MakeProfile(currentStreak: 0, bestStreak: 5);
        var active = MakeProfile(currentStreak: 3, bestStreak: 5);
        var banned = MakeProfile(status: ProfileStatus.Banned, currentStreak: 0, bestStreak: 5);

        var result = AnalyticsAggregator.ComputeFeedToday([broken, active, banned], [], [], [], [], BaseNow);

        Assert.Equal(1, result.StreaksBroken);
    }

    // ─── ComputeStreakOverview ─────────────────────────────────────────

    [Fact]
    public void ComputeStreakOverview_EmptyData_ReturnsZeros()
    {
        var result = AnalyticsAggregator.ComputeStreakOverview([], [], BaseNow);

        Assert.Equal(0, result.LongestStreak);
        Assert.Null(result.LongestProfileId);
        Assert.Equal(0, result.MembersOverSevenDays);
        Assert.Equal(6, result.Distribution.Count);
        // All buckets should be 0
        Assert.All(result.Distribution, b => Assert.Equal(0, b.Value));
    }

    [Fact]
    public void ComputeStreakOverview_DistributionGroupsCorrectly()
    {
        var p1 = MakeProfile(currentStreak: 5);  // "1-6"
        var p2 = MakeProfile(currentStreak: 12); // "11-21"
        var p3 = MakeProfile(currentStreak: 25); // "22-49"
        var p4 = MakeProfile(currentStreak: 0);  // not included (streak == 0)

        var result = AnalyticsAggregator.ComputeStreakOverview([p1, p2, p3, p4], [], BaseNow);

        Assert.Equal(25, result.LongestStreak);
        var dist = result.Distribution.ToDictionary(b => b.Range, b => b.Value);
        Assert.Equal(1, dist["1-6"]);
        Assert.Equal(1, dist["11-21"]);
        Assert.Equal(1, dist["22-49"]);
        Assert.Equal(0, dist["7-10"]);
        Assert.Equal(0, dist["50-89"]);
        Assert.Equal(0, dist["90+"]);
    }

    [Fact]
    public void ComputeStreakOverview_MilestonesThisMonth_CountsFeedEvents()
    {
        var monthStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var events = new List<FeedEvent>
        {
            MakeFeedEvent(FeedEventKind.Hito, monthStart.AddDays(5)),
            MakeFeedEvent(FeedEventKind.Racha, monthStart.AddDays(10)),
            MakeFeedEvent(FeedEventKind.Publicacion, monthStart.AddDays(15)), // not counted
            MakeFeedEvent(FeedEventKind.Hito, monthStart.AddDays(-5)), // previous month
        };
        // Necesitamos al menos un perfil activo no-sistema para que no retorne temprano.
        var profile = MakeProfile();

        var result = AnalyticsAggregator.ComputeStreakOverview([profile], events, BaseNow);

        Assert.Equal(2, result.MilestonesThisMonth);
    }

    // ─── ComputeInactivityDistribution ──────────────────────────────────

    [Fact]
    public void ComputeInactivityDistribution_EmptyData_ReturnsAllZero()
    {
        var result = AnalyticsAggregator.ComputeInactivityDistribution([], BaseNow);
        Assert.Equal(5, result.Count);
        Assert.All(result, b => Assert.Equal(0, b.Value));
    }

    [Fact]
    public void ComputeInactivityDistribution_NullActivity_CountsIn15Plus()
    {
        var noActivity = MakeProfile(lastPostAt: null, lastActiveAt: null);
        var result = AnalyticsAggregator.ComputeInactivityDistribution([noActivity], BaseNow);

        Assert.Equal(1, result.First(b => b.Range == "15+").Value);
        Assert.Equal(0, result.First(b => b.Range == "1-3").Value);
    }

    [Fact]
    public void ComputeInactivityDistribution_ClassifiesCorrectly()
    {
        var now = BaseNow;
        var recent = MakeProfile(lastActiveAt: now.AddDays(-2));   // "1-3"
        var moderate = MakeProfile(lastActiveAt: now.AddDays(-8)); // "7-9"
        var inactive = MakeProfile(lastActiveAt: now.AddDays(-20)); // "15+"

        var result = AnalyticsAggregator.ComputeInactivityDistribution([recent, moderate, inactive], now);

        Assert.Equal(1, result.First(b => b.Range == "1-3").Value);
        Assert.Equal(1, result.First(b => b.Range == "7-9").Value);
        Assert.Equal(1, result.First(b => b.Range == "15+").Value);
        Assert.Equal(0, result.First(b => b.Range == "4-6").Value);
        Assert.Equal(0, result.First(b => b.Range == "10-14").Value);
    }

    // ─── ComputeXpDeliveredSeries ──────────────────────────────────────

    [Fact]
    public void ComputeXpDeliveredSeries_EmptyData_ReturnsFourWeeksZero()
    {
        var result = AnalyticsAggregator.ComputeXpDeliveredSeries([], BaseNow);

        Assert.Equal(4, result.Count);
        Assert.All(result, w =>
        {
            Assert.Equal(0, w.Rachas);
            Assert.Equal(0, w.Posts);
            Assert.Equal(0, w.Erp);
        });
    }

    [Fact]
    public void ComputeXpDeliveredSeries_ClassifiesByReason()
    {
        var now = BaseNow;
        var entries = new List<XpEntry>
        {
            MakeXpEntry(Guid.NewGuid(), 100, "Racha diaria", createdAt: now.AddDays(-3)),
            MakeXpEntry(Guid.NewGuid(), 50, "Post compartido", createdAt: now.AddDays(-2)),
            MakeXpEntry(Guid.NewGuid(), 75, "Puntos XP", createdAt: now.AddDays(-1)),
            MakeXpEntry(Guid.NewGuid(), 30, "BIENVENIDA", createdAt: now.AddDays(-5)),
        };

        var result = AnalyticsAggregator.ComputeXpDeliveredSeries(entries, now);

        // Total rachas: 100, total posts: 50, total erp: 75+30=105
        var totalRachas = result.Sum(w => w.Rachas);
        var totalPosts = result.Sum(w => w.Posts);
        var totalErp = result.Sum(w => w.Erp);

        Assert.Equal(100, totalRachas);
        Assert.Equal(50, totalPosts);
        Assert.Equal(105, totalErp);
    }

    // ─── ComputeMessageReach ───────────────────────────────────────────

    [Fact]
    public void ComputeMessageReach_EmptyData_ReturnsThreeScopes()
    {
        var result = AnalyticsAggregator.ComputeMessageReach([], [], BaseNow);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.Scope == "TODOS");
        Assert.Contains(result, r => r.Scope == "INACTIVOS");
        Assert.Contains(result, r => r.Scope == "ACTIVOS7");
    }

    [Fact]
    public void ComputeMessageReach_Todos_CountsActiveNonSystem()
    {
        var p1 = MakeProfile();
        var p2 = MakeProfile();
        var system = MakeProfile(isSystem: true);
        var banned = MakeProfile(status: ProfileStatus.Banned);

        var result = AnalyticsAggregator.ComputeMessageReach([p1, p2, system, banned], [], BaseNow);

        var todos = result.First(r => r.Scope == "TODOS");
        Assert.Equal(2, todos.Total);
    }

    [Fact]
    public void ComputeMessageReach_Inactivos_MatchesSendBulkCriteria()
    {
        var now = BaseNow;
        var systemProfile = MakeProfile(isSystem: true);

        // Inactive: last activity null or < now-7d
        var inactive = MakeProfile(lastActiveAt: now.AddDays(-10));
        var noActivity = MakeProfile(lastPostAt: null, lastActiveAt: null);
        // Active: last activity within 7d
        var active = MakeProfile(lastActiveAt: now.AddDays(-3));

        var messages = new List<Message>
        {
            MakeMessage(systemProfile.Id, inactive.Id, now.AddHours(-2), triggeredBy: Guid.NewGuid()),
            MakeMessage(systemProfile.Id, active.Id, now.AddHours(-2), triggeredBy: Guid.NewGuid()),
        };

        var result = AnalyticsAggregator.ComputeMessageReach(
            [inactive, noActivity, active, systemProfile], messages, now);

        var inactivos = result.First(r => r.Scope == "INACTIVOS");
        Assert.Equal(2, inactivos.Total); // inactive + noActivity
        Assert.Equal(1, inactivos.Reached); // only inactive got a message

        var activos7 = result.First(r => r.Scope == "ACTIVOS7");
        Assert.Equal(1, activos7.Total);
        Assert.Equal(1, activos7.Reached);
    }
}
