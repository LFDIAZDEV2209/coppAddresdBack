using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Subscriptions;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests de chat grupal (chats grupales): creación/edición de grupos, altas/bajas de
/// miembros, mensajería grupal, consultas y control de acceso de suscripciones.
/// Cada test usa usuarios únicos porque la BD de la colección es compartida.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class GroupChatTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => Npgsql.NpgsqlConnection.ClearAllPools();

    /// <summary>Crea dos perfiles (Ana/me y Bruno/peer) como amigos mutuos.</summary>
    private async Task<(Guid MeUser, Guid PeerUser, Profile Me, Profile Peer)> SeedMutualAsync(
        CommunityDbContext db, string meName = "Ana", string peerName = "Bruno")
    {
        var meUser = Guid.NewGuid();
        var peerUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, meName);
        var peer = await CommunityTestData.SeedProfileAsync(db, peerUser, peerName);
        await CommunityTestData.SeedFollowAsync(db, me.Id, peer.Id);
        await CommunityTestData.SeedFollowAsync(db, peer.Id, me.Id);
        return (meUser, peerUser, me, peer);
    }

    private static async Task<ChatGroup> CreateGroupAsync(
        Guid creatorUser, Profile creator, Profile member, CommunityDbContext db)
    {
        var sender = new RecordingTopicEventSender();
        return await new CommunityMutation().CreateGroup(
            "Grupo de prueba", new List<Guid> { member.Id }, db,
            CommunityTestData.HttpAs(creatorUser), sender, CancellationToken.None);
    }

    // --- Creación ---

    [Fact]
    public async Task CreateGroup_RequiresMutualFriendMembers()
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana");
        var stranger = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Bruno");

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().CreateGroup(
            "Grupo de prueba", new List<Guid> { stranger.Id }, db,
            CommunityTestData.HttpAs(meUser), new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("amigos", ex.Message);
    }

    [Fact]
    public async Task CreateGroup_RejectsInvalidName()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().CreateGroup(
            "ab", new List<Guid> { peer.Id }, db,
            CommunityTestData.HttpAs(meUser), new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("El nombre del grupo debe tener entre 3 y 100 caracteres", ex.Message);
    }

    [Fact]
    public async Task CreateGroup_AddsCreatorAsMember_AndPublishesChangedTopic()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var sender = new RecordingTopicEventSender();

        var group = await new CommunityMutation().CreateGroup(
            "Amigos", new List<Guid> { peer.Id }, db,
            CommunityTestData.HttpAs(meUser), sender, CancellationToken.None);

        Assert.Equal(2, await db.ChatGroupMembers.CountAsync(m => m.GroupId == group.Id));
        Assert.True(await db.ChatGroupMembers.AnyAsync(m => m.GroupId == group.Id && m.ProfileId == me.Id));
        Assert.True(await db.ChatGroupMembers.AnyAsync(m => m.GroupId == group.Id && m.ProfileId == peer.Id));

        var evt = Assert.Single(sender.Sent);
        Assert.Equal($"group_{group.Id}_changed", evt.Topic);
    }

    // --- Mensajería grupal ---

    [Fact]
    public async Task SendGroupMessage_RejectsNonMember()
    {
        var (meUser, peerUser, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);
        var outsiderUser = Guid.NewGuid();
        await CommunityTestData.SeedProfileAsync(db, outsiderUser, "Carla");

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().SendGroupMessage(
            group.Id, "hola", db, CommunityTestData.HttpAs(outsiderUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("No eres miembro de este grupo", ex.Message);
    }

    [Fact]
    public async Task SendGroupMessage_PublishesToGroupTopic()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);
        var sender = new RecordingTopicEventSender();

        var sent = await new CommunityMutation().SendGroupMessage(
            group.Id, "¿Qué tal?", db, CommunityTestData.HttpAs(meUser), sender, CancellationToken.None);

        Assert.Equal(group.Id, sent.ConversationId);
        Assert.Null(sent.RecipientProfileId);
        Assert.Contains(sender.Sent, s => s.Topic == $"group_{group.Id}");
    }

    [Fact]
    public async Task GroupMessageAdded_RejectsNonMember()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);
        var outsiderUser = Guid.NewGuid();
        await CommunityTestData.SeedProfileAsync(db, outsiderUser, "Carla");

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            CommunityQuery.RequireGroupMembershipAsync(db, CommunityTestData.HttpAs(outsiderUser), group.Id, CancellationToken.None));

        Assert.Contains("No eres miembro de este grupo", ex.Message);
    }

    // --- Consultas ---

    [Fact]
    public async Task Groups_ReturnsMyGroups_WithLastMessageAndMemberCount()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var friendUser2 = Guid.NewGuid();
        var friend2 = await CommunityTestData.SeedProfileAsync(db, friendUser2, "Carla");
        await CommunityTestData.SeedFollowAsync(db, me.Id, friend2.Id);
        await CommunityTestData.SeedFollowAsync(db, friend2.Id, me.Id);

        var groupA = await CreateGroupAsync(meUser, me, peer, db);
        var groupB = await CreateGroupAsync(meUser, me, friend2, db);

        // Un mensaje solo en el grupo A.
        await new CommunityMutation().SendGroupMessage(
            groupA.Id, "mensaje A", db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None);

        var result = (await new CommunityQuery().Groups(
            db, CommunityTestData.HttpAs(meUser), CancellationToken.None)).ToList();

        Assert.Equal(2, result.Count);
        // El grupo con mensaje más reciente va primero.
        Assert.Equal(groupA.Id, result[0].Id);
        Assert.Equal(2, result[0].MemberCount);
        Assert.NotNull(result[0].LastMessage);
        Assert.Equal("mensaje A", result[0].LastMessage!.Body);
        Assert.Equal(groupB.Id, result[1].Id);
        Assert.Equal(2, result[1].MemberCount);
        Assert.Null(result[1].LastMessage);
    }

    [Fact]
    public async Task Conversations_ExcludesGroupMessages()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();

        // Mensaje 1:1 (conversation_id nulo).
        await CommunityTestData.SeedMessageAsync(db, me.Id, peer.Id, "1:1", DateTime.UtcNow, CancellationToken.None);

        // Mensaje de grupo (conversation_id seteado).
        var group = await CreateGroupAsync(meUser, me, peer, db);
        await new CommunityMutation().SendGroupMessage(
            group.Id, "grupal", db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None);

        var conversations = await new CommunityQuery().Conversations(
            db, CommunityTestData.HttpAs(meUser), CancellationToken.None);

        Assert.Single(conversations);
        Assert.Equal("1:1", conversations[0].LastMessage!.Body);
    }

    [Fact]
    public async Task Group_ReturnsHistory_ForMember()
    {
        var (meUser, peerUser, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);
        await new CommunityMutation().SendGroupMessage(
            group.Id, "hola grupo", db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None);

        var history = await new CommunityQuery().Group(
            group.Id, db, CommunityTestData.HttpAs(peerUser), CancellationToken.None);

        Assert.Single(history);
        Assert.Equal("hola grupo", history[0].Body);
    }

    [Fact]
    public async Task GroupMembers_ReturnsAllMembers()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);

        var members = await new CommunityQuery().GroupMembers(
            group.Id, db, CommunityTestData.HttpAs(meUser), CancellationToken.None);

        Assert.Equal(2, members.Count);
    }

    // --- Gestión de miembros ---

    [Fact]
    public async Task LeaveGroup_DeletesGroupWhenEmpty()
    {
        var (meUser, peerUser, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);
        var sender = new RecordingTopicEventSender();

        // El otro miembro sale primero (queda solo el creador).
        await new CommunityMutation().LeaveGroup(
            group.Id, db, CommunityTestData.HttpAs(peerUser), sender, CancellationToken.None);
        Assert.True(await db.ChatGroups.AnyAsync(g => g.Id == group.Id));

        // El creador sale y el grupo queda vacío → se elimina.
        await new CommunityMutation().LeaveGroup(
            group.Id, db, CommunityTestData.HttpAs(meUser), sender, CancellationToken.None);

        Assert.False(await db.ChatGroups.AnyAsync(g => g.Id == group.Id));
        Assert.False(await db.ChatGroupMembers.AnyAsync(m => m.GroupId == group.Id));
    }

    [Fact]
    public async Task RemoveGroupMember_CannotRemoveCreator()
    {
        var (meUser, peerUser, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().RemoveGroupMember(
            group.Id, me.Id, db, CommunityTestData.HttpAs(peerUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("No puedes quitar al creador del grupo", ex.Message);
    }

    [Fact]
    public async Task AddGroupMember_RequiresMutualFriendOfAdder()
    {
        var (meUser, peerUser, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        // "Carla" existe y está activa, pero NO es amiga mutua de Bruno (el que añade).
        var outsiderUser = Guid.NewGuid();
        await CommunityTestData.SeedProfileAsync(db, outsiderUser, "Carla");
        var group = await CreateGroupAsync(meUser, me, peer, db);
        var outsiderId = await db.Profiles.Where(p => p.UserId == outsiderUser).Select(p => p.Id).SingleAsync();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().AddGroupMember(
            group.Id, outsiderId,
            db, CommunityTestData.HttpAs(peerUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("amigos", ex.Message);
    }

    [Fact]
    public async Task RenameGroup_RejectsNonMember()
    {
        var (meUser, _, me, peer) = await SeedMutualAsync(_factory.Create());
        await using var db = _factory.Create();
        var group = await CreateGroupAsync(meUser, me, peer, db);
        var outsiderUser = Guid.NewGuid();
        await CommunityTestData.SeedProfileAsync(db, outsiderUser, "Carla");

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().RenameGroup(
            group.Id, "Nuevo nombre", db, CommunityTestData.HttpAs(outsiderUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("No eres miembro de este grupo", ex.Message);
    }
}
