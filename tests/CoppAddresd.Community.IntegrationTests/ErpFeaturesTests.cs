using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests de las nuevas features ERP de comunidad: XP/niveles, eventos de feed, ranking de
/// rachas, creación de publicaciones con tipo/destino y visualizaciones. Requieren PostgreSQL
/// (COP_TEST_DB_CONNECTION); se omiten si no está disponible.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class ErpFeaturesTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => NpgsqlConnection.ClearAllPools();

    private static IHttpContextAccessor HttpAs(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    private static IHttpContextAccessor HttpAs(Guid userId, string name)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, name),
        ], "test");
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    [Fact]
    public async Task AwardXp_CreatesEntry_AndUpdatesXpTotal()
    {
        var db = _factory.Create();
        await using var _ = db;
        var profile = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Ana López", CancellationToken.None);
        var sender = new RecordingTopicEventSender();

        var result = await new CommunityMutation().AwardXp(
            profile.Id, 500, "Racha destacada", db, sender, HttpAs(Guid.NewGuid()), CancellationToken.None);

        Assert.Single(result);
        var reloaded = await db.Profiles.FirstAsync(p => p.Id == profile.Id, CancellationToken.None);
        Assert.Equal(500, reloaded.XpTotal);
        Assert.Equal(1, await db.XpEntries.CountAsync(x => x.ProfileId == profile.Id, CancellationToken.None));
        Assert.Contains(sender.Sent, s => s.Topic == "feed_event_added");
    }

    [Fact]
    public async Task AwardXpToAll_AwardsEveryActiveProfile()
    {
        var db = _factory.Create();
        await using var _ = db;
        var a = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "A", CancellationToken.None);
        var b = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "B", CancellationToken.None);
        var sender = new RecordingTopicEventSender();

        var result = await new CommunityMutation().AwardXpToAll(100, "Bonus", db, sender, HttpAs(Guid.NewGuid()), CancellationToken.None);

        Assert.Contains(result, p => p.DisplayName == "A");
        Assert.Contains(result, p => p.DisplayName == "B");
        Assert.Equal(1, await db.XpEntries.CountAsync(x => x.ProfileId == a.Id, CancellationToken.None));
        Assert.Equal(1, await db.XpEntries.CountAsync(x => x.ProfileId == b.Id, CancellationToken.None));
    }

    [Fact]
    public async Task FeedEvents_ReturnsMostRecentFirst()
    {
        var db = _factory.Create();
        await using var _ = db;
        var profile = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Feeds", CancellationToken.None);
        db.FeedEvents.Add(new FeedEvent { Id = Guid.NewGuid(), ProfileId = profile.Id, Kind = FeedEventKind.Racha, CreatedAt = DateTime.UtcNow.AddMinutes(-10) });
        db.FeedEvents.Add(new FeedEvent { Id = Guid.NewGuid(), ProfileId = profile.Id, Kind = FeedEventKind.Logro, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(CancellationToken.None);

        var events = (await new CommunityQuery().FeedEvents(db, 100, 0, CancellationToken.None))
            .Where(e => e.ProfileId == profile.Id)
            .ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal(FeedEventKind.Logro, events[0].Kind);
    }

    [Fact]
    public async Task TopStreaks_OrdersByStreakThenXp()
    {
        var db = _factory.Create();
        await using var _ = db;
        var high = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "High", CancellationToken.None);
        high.CurrentStreak = 5;
        high.XpTotal = 200;
        var low = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Low", CancellationToken.None);
        low.CurrentStreak = 2;
        low.XpTotal = 9000;
        await db.SaveChangesAsync(CancellationToken.None);

        var ranking = await new CommunityQuery().TopStreaks(db, 100, CancellationToken.None);
        var idxHigh = ranking.FindIndex(p => p.DisplayName == "High");
        var idxLow = ranking.FindIndex(p => p.DisplayName == "Low");

        Assert.True(idxHigh >= 0 && idxLow >= 0);
        Assert.True(idxHigh < idxLow);
    }

    [Fact]
    public async Task CreatePost_StoresTypeAndDestination()
    {
        var db = _factory.Create();
        await using var _ = db;
        var user = Guid.NewGuid();
        var profile = await CommunityTestData.SeedProfileAsync(db, user, "Poster", CancellationToken.None);
        var sender = new RecordingTopicEventSender();

        var post = await new CommunityMutation().CreatePost(
            "Hola", PostType.Imagen, PostDestination.CocinaSaludable, db, HttpAs(user), sender, null, CancellationToken.None);

        Assert.Equal(PostType.Imagen, post.Type);
        Assert.Equal(PostDestination.CocinaSaludable, post.Destination);
        Assert.Contains(sender.Sent, s => s.Topic == "post_added");
        Assert.Contains(sender.Sent, s => s.Topic == "feed_event_added");
    }

    [Fact]
    public async Task ViewPost_IncrementsViewCount()
    {
        var db = _factory.Create();
        await using var _ = db;
        var profile = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Viewer", CancellationToken.None);
        var post = new Post { Id = Guid.NewGuid(), ProfileId = profile.Id, Body = "x", CreatedAt = DateTime.UtcNow };
        db.Posts.Add(post);
        await db.SaveChangesAsync(CancellationToken.None);

        var updated = await new CommunityMutation().ViewPost(post.Id, db, CancellationToken.None);

        Assert.Equal(1, updated!.ViewCount);
    }

    [Fact]
    public async Task Me_UsesJwtNameClaim_ForDisplayName()
    {
        var db = _factory.Create();
        await using var _ = db;
        var user = Guid.NewGuid();
        var http = HttpAs(user, "Carolina Mendoza");

        var profile = await new CommunityQuery().Me(db, http, CancellationToken.None);

        Assert.Equal("Carolina Mendoza", profile!.DisplayName);
    }
}
