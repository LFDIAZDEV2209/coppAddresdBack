using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Metrics;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Tests del backfill/reconciliación del rollup de métricas del dashboard ERP
/// (Dashboard #5): equivalencia exacta contra el OLTP (incluye soft-deleted),
/// limpieza de celdas obsoletas/erróneas e idempotencia. Igual que el resto de
/// tests de métricas, requieren PostgreSQL real (<c>COP_TEST_DB_CONNECTION</c>)
/// sobre una BD aislada (<c>coppaddresd_comm_metrics_test_*</c>); sin la
/// variable, se reportan SKIPPED.
/// </summary>
public sealed class CommunityMetricsBackfillTests : IClassFixture<CommunityMetricsProcessorTestDb>
{
    private readonly CommunityMetricsProcessorTestDb _db;

    public CommunityMetricsBackfillTests(CommunityMetricsProcessorTestDb db) => _db = db;

    [RequiresPostgresFact]
    public async Task Recompute_Replicates_Oltp_Counts_IncludingSoftDeleted()
    {
        var date = new DateOnly(2026, 5, 10);

        await using (var seed = CreateDb())
        {
            var profile = NewProfile();
            seed.Profiles.Add(profile);

            var p1 = NewPost(profile.Id, PostType.Texto, At(date, 10, 15));
            var p2 = NewPost(profile.Id, PostType.Imagen, At(date, 10, 45));
            var p3 = NewPost(profile.Id, PostType.Encuesta, At(date, 14, 5));
            var p4 = NewPost(profile.Id, PostType.Video, At(date, 5, 30));
            p4.DeletedAt = At(date, 6, 0);
            var p5 = NewPost(profile.Id, PostType.Logro, At(date, 16, 0));
            seed.Posts.AddRange(p1, p2, p3, p4, p5);

            var c1 = NewComment(p1.Id, profile.Id, At(date, 10, 20));
            var c2 = NewComment(p2.Id, profile.Id, At(date, 11, 0));
            c2.DeletedAt = At(date, 12, 0);
            seed.Comments.AddRange(c1, c2);

            seed.Likes.Add(NewPostLike(p1.Id, profile.Id, At(date, 10, 25)));
            seed.Likes.Add(NewCommentLike(c1.Id, profile.Id, At(date, 23, 59)));
            seed.Reposts.Add(new Repost
            {
                Id = Guid.NewGuid(),
                PostId = p1.Id,
                ProfileId = profile.Id,
                CreatedAt = At(date, 23, 30),
            });

            await seed.SaveChangesAsync();
        }

        await using (var db = CreateDb())
        {
            await CommunityMetricsBackfillSql.RecomputeAllAsync(db);
        }

        var rows = await ReadDateAsync(date);

        // posts_count: total + por tipo, incluyendo el post soft-deleted (el rollup no decrementa al eliminar).
        Assert.Equal(5, Get(rows, "posts_count", "total"));
        Assert.Equal(1, Get(rows, "posts_count", "texto"));
        Assert.Equal(1, Get(rows, "posts_count", "imagen"));
        Assert.Equal(1, Get(rows, "posts_count", "video"));
        Assert.Equal(1, Get(rows, "posts_count", "poll"));
        Assert.Equal(1, Get(rows, "posts_count", "logro"));

        // comments_count incluye el comentario soft-deleted.
        Assert.Equal(2, Get(rows, "comments_count", "total"));
        Assert.Equal(2, Get(rows, "likes_count", "total"));
        Assert.Equal(1, Get(rows, "reposts_count", "total"));

        // hourly_activity = posts + comentarios + reposts por hora UTC, dimensión SIN padding.
        Assert.Equal(1, Get(rows, "hourly_activity", "5"));
        Assert.Equal(3, Get(rows, "hourly_activity", "10"));
        Assert.Equal(1, Get(rows, "hourly_activity", "11"));
        Assert.Equal(1, Get(rows, "hourly_activity", "14"));
        Assert.Equal(1, Get(rows, "hourly_activity", "16"));
        Assert.Equal(1, Get(rows, "hourly_activity", "23"));

        Assert.DoesNotContain(rows, x => x.MetricKey == "hourly_activity" && x.DimensionKey == "05");
        Assert.DoesNotContain(rows, x => x.DimensionKey == "encuesta");
    }

    [RequiresPostgresFact]
    public async Task Recompute_MapsNullTypeToGeneral_AndEncuestaToPoll()
    {
        var date = new DateOnly(2026, 5, 11);

        await using (var seed = CreateDb())
        {
            var profile = NewProfile();
            seed.Profiles.Add(profile);
            seed.Posts.Add(NewPost(profile.Id, null, At(date, 8, 0)));
            seed.Posts.Add(NewPost(profile.Id, PostType.Encuesta, At(date, 8, 30)));
            await seed.SaveChangesAsync();
        }

        await using (var db = CreateDb())
        {
            await CommunityMetricsBackfillSql.RecomputeAllAsync(db);
        }

        var rows = await ReadDateAsync(date);

        Assert.Equal(2, Get(rows, "posts_count", "total"));
        Assert.Equal(1, Get(rows, "posts_count", "general"));
        Assert.Equal(1, Get(rows, "posts_count", "poll"));
        Assert.Equal(2, Get(rows, "hourly_activity", "8"));
        Assert.DoesNotContain(rows, x => x.DimensionKey is "encuesta" or "Encuesta");
    }

    [RequiresPostgresFact]
    public async Task Recompute_DeletesStaleAndBogusCells()
    {
        var date = new DateOnly(2026, 5, 12);
        var emptyDate = new DateOnly(2026, 5, 13);
        // last_updated_at es timestamp without time zone: Npgsql rechaza Kind=UTC.
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using (var seed = CreateDb())
        {
            var profile = NewProfile();
            seed.Profiles.Add(profile);
            seed.Posts.Add(NewPost(profile.Id, PostType.Texto, At(date, 9, 10)));

            // Celdas obsoletas/erróneas: el rebuild debe corregirlas o eliminarlas.
            seed.CommunityDailyMetrics.AddRange(
                new CommunityDailyMetric { MetricDate = date, MetricKey = "likes_count", DimensionKey = "total", TotalCount = 999, LastUpdatedAt = now },
                new CommunityDailyMetric { MetricDate = date, MetricKey = "posts_count", DimensionKey = "total", TotalCount = 0, LastUpdatedAt = now },
                new CommunityDailyMetric { MetricDate = date, MetricKey = "posts_count", DimensionKey = "encuesta", TotalCount = 7, LastUpdatedAt = now },
                new CommunityDailyMetric { MetricDate = date, MetricKey = "hourly_activity", DimensionKey = "05", TotalCount = 42, LastUpdatedAt = now },
                new CommunityDailyMetric { MetricDate = emptyDate, MetricKey = "posts_count", DimensionKey = "total", TotalCount = 3, LastUpdatedAt = now },
                new CommunityDailyMetric { MetricDate = emptyDate, MetricKey = "likes_count", DimensionKey = "total", TotalCount = 5, LastUpdatedAt = now });

            await seed.SaveChangesAsync();
        }

        await using (var db = CreateDb())
        {
            await CommunityMetricsBackfillSql.RecomputeAllAsync(db);
        }

        var rows = await ReadDateAsync(date);
        var emptyRows = await ReadDateAsync(emptyDate);

        // Valor stale corregido y celda de una métrica que puede encogerse que ya no existe.
        Assert.Equal(1, Get(rows, "posts_count", "total"));
        Assert.Null(Get(rows, "likes_count", "total"));

        // Dimensión legacy y dimensión con padding eliminadas; la hora correcta existe.
        Assert.DoesNotContain(rows, x => x.DimensionKey is "encuesta" or "05");
        Assert.Equal(1, Get(rows, "hourly_activity", "9"));

        // Fecha sin actividad OLTP: no queda ninguna celda del rollup.
        Assert.Empty(emptyRows);
    }

    [RequiresPostgresFact]
    public async Task Recompute_RunTwice_IsIdempotent()
    {
        var date = new DateOnly(2026, 5, 14);

        await using (var seed = CreateDb())
        {
            var profile = NewProfile();
            seed.Profiles.Add(profile);

            var post = NewPost(profile.Id, PostType.Logro, At(date, 7, 15));
            seed.Posts.Add(post);

            var comment = NewComment(post.Id, profile.Id, At(date, 7, 30));
            seed.Comments.Add(comment);

            seed.Likes.Add(NewPostLike(post.Id, profile.Id, At(date, 7, 45)));
            seed.Likes.Add(NewCommentLike(comment.Id, profile.Id, At(date, 7, 50)));
            seed.Reposts.Add(new Repost
            {
                Id = Guid.NewGuid(),
                PostId = post.Id,
                ProfileId = profile.Id,
                CreatedAt = At(date, 7, 55),
            });

            await seed.SaveChangesAsync();
        }

        await using (var db = CreateDb())
        {
            await CommunityMetricsBackfillSql.RecomputeAllAsync(db);
        }
        var first = await SnapshotAsync();

        await using (var db = CreateDb())
        {
            await CommunityMetricsBackfillSql.RecomputeAllAsync(db);
        }
        var second = await SnapshotAsync();

        Assert.Equal(first, second);
        Assert.Equal(2, Get(await ReadDateAsync(date), "likes_count", "total"));
    }

    [RequiresPostgresFact]
    public async Task BackfillService_DryRun_RollsBack_And_RealRun_CleansStale()
    {
        var date = new DateOnly(2026, 5, 15);
        // last_updated_at es timestamp without time zone: Npgsql rechaza Kind=UTC.
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using (var seed = CreateDb())
        {
            var profile = NewProfile();
            seed.Profiles.Add(profile);

            var post = NewPost(profile.Id, PostType.Texto, At(date, 13, 0));
            seed.Posts.Add(post);

            var comment = NewComment(post.Id, profile.Id, At(date, 13, 5));
            seed.Comments.Add(comment);

            seed.Likes.Add(NewPostLike(post.Id, profile.Id, At(date, 13, 10)));
            seed.Reposts.Add(new Repost
            {
                Id = Guid.NewGuid(),
                PostId = post.Id,
                ProfileId = profile.Id,
                CreatedAt = At(date, 13, 15),
            });

            // Celda obsoleta: no hay likes "fantasma", el rebuild debe eliminarla.
            seed.CommunityDailyMetrics.Add(new CommunityDailyMetric
            {
                MetricDate = date,
                MetricKey = "likes_count",
                DimensionKey = "total",
                TotalCount = 999,
                LastUpdatedAt = now,
            });

            await seed.SaveChangesAsync();
        }

        await using var db = CreateDb();
        var service = new CommunityMetricsBackfillService(
            db, NullLogger<CommunityMetricsBackfillService>.Instance);

        var dry = await service.BackfillAsync(dryRun: true, CancellationToken.None);

        Assert.True(dry.DryRun);
        Assert.Contains("Simulación", dry.Note);
        Assert.Equal(dry.RowsPerKey.Values.Sum(), dry.InsertedRows);
        Assert.Contains(CommunityMetricsBackfillSql.PostsKey, dry.RowsPerKey.Keys);
        Assert.Contains(CommunityMetricsBackfillSql.CommentsKey, dry.RowsPerKey.Keys);
        Assert.Contains(CommunityMetricsBackfillSql.LikesKey, dry.RowsPerKey.Keys);
        Assert.Contains(CommunityMetricsBackfillSql.RepostsKey, dry.RowsPerKey.Keys);
        Assert.Contains(CommunityMetricsBackfillSql.HourlyKey, dry.RowsPerKey.Keys);

        // El rollback dejó intacta la celda obsoleta.
        Assert.Equal(999, (await FindMetricAsync(date, "likes_count", "total"))?.TotalCount);

        var real = await service.BackfillAsync(dryRun: false, CancellationToken.None);

        Assert.False(real.DryRun);
        Assert.Contains("converger", real.Note);
        Assert.Equal(1, (await FindMetricAsync(date, "likes_count", "total"))?.TotalCount);
        Assert.Equal(1, (await FindMetricAsync(date, "posts_count", "total"))?.TotalCount);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private CommunityDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CommunityDbContext>()
            .UseNpgsql(_db.ConnectionString, n =>
                n.MigrationsHistoryTable("__EFMigrationsHistory", "community"))
            .Options);

    private static Profile NewProfile() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        DisplayName = "Perfil de prueba de backfill",
        Status = ProfileStatus.Active,
        CreatedAt = DateTime.UtcNow,
    };

    private static Post NewPost(Guid profileId, PostType? type, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        ProfileId = profileId,
        Body = "publicación de prueba",
        Type = type,
        Destination = PostDestination.TodasLasComunidades,
        CreatedAt = createdAt,
    };

    private static Comment NewComment(Guid postId, Guid profileId, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        PostId = postId,
        ProfileId = profileId,
        Body = "comentario de prueba",
        CreatedAt = createdAt,
    };

    private static Like NewPostLike(Guid postId, Guid profileId, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        PostId = postId,
        ProfileId = profileId,
        CreatedAt = createdAt,
    };

    private static Like NewCommentLike(Guid commentId, Guid profileId, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        CommentId = commentId,
        ProfileId = profileId,
        CreatedAt = createdAt,
    };

    private static DateTime At(DateOnly date, int hour, int minute) =>
        new(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Utc);

    private async Task<IReadOnlyList<CommunityDailyMetric>> ReadDateAsync(DateOnly date)
    {
        await using var db = CreateDb();
        return await db.CommunityDailyMetrics.AsNoTracking()
            .Where(x => x.MetricDate == date)
            .OrderBy(x => x.MetricKey)
            .ThenBy(x => x.DimensionKey)
            .ToListAsync();
    }

    private async Task<CommunityDailyMetric?> FindMetricAsync(DateOnly date, string key, string dimension)
    {
        await using var db = CreateDb();
        return await db.CommunityDailyMetrics.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.MetricDate == date && x.MetricKey == key && x.DimensionKey == dimension);
    }

    private async Task<List<(DateOnly Date, string Key, string Dimension, long Count)>> SnapshotAsync()
    {
        await using var db = CreateDb();
        return await db.CommunityDailyMetrics.AsNoTracking()
            .Where(x => CommunityMetricsBackfillSql.MetricKeys.Contains(x.MetricKey))
            .OrderBy(x => x.MetricDate)
            .ThenBy(x => x.MetricKey)
            .ThenBy(x => x.DimensionKey)
            .Select(x => new ValueTuple<DateOnly, string, string, long>(
                x.MetricDate, x.MetricKey, x.DimensionKey, x.TotalCount))
            .ToListAsync();
    }

    private static long? Get(IReadOnlyList<CommunityDailyMetric> rows, string key, string dimension)
        => rows.FirstOrDefault(x => x.MetricKey == key && x.DimensionKey == dimension)?.TotalCount;
}
