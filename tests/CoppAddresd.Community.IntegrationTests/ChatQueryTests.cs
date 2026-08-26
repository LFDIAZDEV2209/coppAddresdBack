using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests de las consultas de chat: resumen de conversaciones (último mensaje por
/// par, ordenado por actividad) e historial de una conversación (solo participantes).
/// Cada test siembra usuarios únicos porque la BD de la colección es compartida.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class ChatQueryTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => Npgsql.NpgsqlConnection.ClearAllPools();

    private static readonly DateTime Base = new(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Siembra 3 usuarios únicos (Ana↔Bruno×3, Ana↔Carla×1, Bruno↔Carla×1).</summary>
    private async Task<(CommunityDbContext Db, Guid MeUser, Guid PeerBUser, Guid PeerCUser)>
        SeedChatAsync(CancellationToken ct = default)
    {
        var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var peerBUser = Guid.NewGuid();
        var peerCUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana", ct);
        var peerB = await CommunityTestData.SeedProfileAsync(db, peerBUser, "Bruno", ct);
        var peerC = await CommunityTestData.SeedProfileAsync(db, peerCUser, "Carla", ct);

        // A ↔ B: tres mensajes.
        await CommunityTestData.SeedMessageAsync(db, me.Id, peerB.Id, "b1", Base.AddMinutes(1), ct);
        await CommunityTestData.SeedMessageAsync(db, peerB.Id, me.Id, "b2", Base.AddMinutes(2), ct);
        await CommunityTestData.SeedMessageAsync(db, me.Id, peerB.Id, "b3", Base.AddMinutes(3), ct);
        // A ↔ C: un mensaje, posterior a todos.
        await CommunityTestData.SeedMessageAsync(db, me.Id, peerC.Id, "c1", Base.AddMinutes(4), ct);
        // B ↔ C (ajeno a A, no debe aparecer en las conversaciones de A).
        await CommunityTestData.SeedMessageAsync(db, peerB.Id, peerC.Id, "bc", Base.AddMinutes(5), ct);

        return (db, meUser, peerBUser, peerCUser);
    }

    [Fact]
    public async Task Conversations_ReturnsOnePerPeer_WithLatestMessage()
    {
        var (db, meUser, _, _) = await SeedChatAsync();
        await using var _ = db;

        var conversations = await new CommunityQuery().Conversations(
            db, CommunityTestData.HttpAs(meUser), CancellationToken.None);

        Assert.Equal(2, conversations.Count);
        // Orden por actividad: la de Carla (más reciente) primero.
        Assert.Equal("Carla", conversations[0].Peer.DisplayName);
        Assert.Equal("c1", conversations[0].LastMessage!.Body);
        Assert.Equal("Bruno", conversations[1].Peer.DisplayName);
        Assert.Equal("b3", conversations[1].LastMessage!.Body);
    }

    [Fact]
    public async Task Conversations_AppliesTakeAndSkip()
    {
        var (db, meUser, _, _) = await SeedChatAsync();
        await using var _ = db;

        var first = await new CommunityQuery().Conversations(
            db, CommunityTestData.HttpAs(meUser), CancellationToken.None, take: 1);
        var second = await new CommunityQuery().Conversations(
            db, CommunityTestData.HttpAs(meUser), CancellationToken.None, take: 1, skip: 1);

        Assert.Single(first);
        Assert.Equal("Carla", first[0].Peer.DisplayName);
        Assert.Single(second);
        Assert.Equal("Bruno", second[0].Peer.DisplayName);
    }

    [Fact]
    public async Task Conversation_ReturnsOnlyThatPeer_OrderedNewestFirst()
    {
        var (db, meUser, peerBUser, _) = await SeedChatAsync();
        await using var _ = db;
        var me = await db.Profiles.SingleAsync(p => p.UserId == meUser);
        var peerB = await db.Profiles.SingleAsync(p => p.UserId == peerBUser);

        var history = await new CommunityQuery().Conversation(
            peerB.Id, db, CommunityTestData.HttpAs(meUser), CancellationToken.None);

        var bodies = history.Select(m => m.Body).ToArray();
        Assert.Equal(["b3", "b2", "b1"], bodies);
    }

    [Fact]
    public async Task Conversation_FromPeerSide_WorksSymmetric()
    {
        var (db, meUser, peerBUser, _) = await SeedChatAsync();
        await using var _ = db;
        var me = await db.Profiles.SingleAsync(p => p.UserId == meUser);
        var peerB = await db.Profiles.SingleAsync(p => p.UserId == peerBUser);

        var history = await new CommunityQuery().Conversation(
            me.Id, db, CommunityTestData.HttpAs(peerBUser), CancellationToken.None);

        Assert.Equal(3, history.Count);
        Assert.All(history, m =>
            Assert.True((m.SenderProfileId == me.Id && m.RecipientProfileId == peerB.Id)
                     || (m.SenderProfileId == peerB.Id && m.RecipientProfileId == me.Id)));
    }

    [Fact]
    public async Task Conversation_ExcludesMessagesWithOtherPeers()
    {
        var (db, meUser, _, peerCUser) = await SeedChatAsync();
        await using var _ = db;
        var me = await db.Profiles.SingleAsync(p => p.UserId == meUser);
        var peerC = await db.Profiles.SingleAsync(p => p.UserId == peerCUser);

        // Conversación de C con A: no debe incluir los mensajes B↔C ni A↔B.
        var history = await new CommunityQuery().Conversation(
            me.Id, db, CommunityTestData.HttpAs(peerCUser), CancellationToken.None);

        var bodies = history.Select(m => m.Body).ToArray();
        Assert.Equal(["c1"], bodies);
    }
}
