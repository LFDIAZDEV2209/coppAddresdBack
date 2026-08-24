using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests de la mutación SendMessage (reglas de negocio del chat 1:1):
/// mutual-follow obligatorio, validación de contenido y publicación del tópico.
/// Cada test usa usuarios únicos porque la BD de la colección es compartida.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class SendMessageTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => Npgsql.NpgsqlConnection.ClearAllPools();

    private async Task<(Guid MeUser, Guid PeerUser, Guid MeProfile, Guid PeerProfile)>
        SeedMutualFriendsAsync(CancellationToken ct = default)
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var peerUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana", ct);
        var peer = await CommunityTestData.SeedProfileAsync(db, peerUser, "Bruno", ct);
        await CommunityTestData.SeedFollowAsync(db, me.Id, peer.Id, ct);
        await CommunityTestData.SeedFollowAsync(db, peer.Id, me.Id, ct);
        return (meUser, peerUser, me.Id, peer.Id);
    }

    [Fact]
    public async Task SendMessage_Rejects_WhenNoMutualFollow()
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana");
        var peer = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Bruno");

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().SendMessage(
            peer.Id, "hola", db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("amigos", ex.Message);
    }

    [Fact]
    public async Task SendMessage_Rejects_EmptyBody()
    {
        var (meUser, _, _, peerProfile) = await SeedMutualFriendsAsync();
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().SendMessage(
            peerProfile, "   ", db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("Escribe un mensaje", ex.Message);
    }

    [Fact]
    public async Task SendMessage_Rejects_BodyOver1000()
    {
        var (meUser, _, _, peerProfile) = await SeedMutualFriendsAsync();
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().SendMessage(
            peerProfile, new string('x', 1001), db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("1000", ex.Message);
    }

    [Fact]
    public async Task SendMessage_Allows_BodyOfExactly1000()
    {
        var (meUser, _, _, peerProfile) = await SeedMutualFriendsAsync();
        await using var db = _factory.Create();

        var sent = await new CommunityMutation().SendMessage(
            peerProfile, new string('x', 1000), db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None);

        Assert.Equal(1000, sent.Body.Length);
    }

    [Fact]
    public async Task SendMessage_Rejects_UnknownRecipient()
    {
        var (meUser, _, _, _) = await SeedMutualFriendsAsync();
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().SendMessage(
            Guid.NewGuid(), "hola", db, CommunityTestData.HttpAs(meUser),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("destinatario", ex.Message);
    }

    [Fact]
    public async Task SendMessage_Rejects_WhenSenderHasNoProfile()
    {
        var (_, _, _, peerProfile) = await SeedMutualFriendsAsync();
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().SendMessage(
            peerProfile, "hola", db, CommunityTestData.HttpAs(Guid.NewGuid()),
            new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("perfil", ex.Message);
    }

    [Fact]
    public async Task SendMessage_SavesAndPublishesTopic_ToSortedConversationKey()
    {
        var (meUser, _, meProfile, peerProfile) = await SeedMutualFriendsAsync();
        await using var db = _factory.Create();
        var sender = new RecordingTopicEventSender();

        var sent = await new CommunityMutation().SendMessage(
            peerProfile, "¿Cómo estás?", db, CommunityTestData.HttpAs(meUser),
            sender, CancellationToken.None);

        Assert.Equal(meProfile, sent.SenderProfileId);
        Assert.Equal(peerProfile, sent.RecipientProfileId);
        Assert.Equal("¿Cómo estás?", sent.Body);

        var persisted = await db.Messages.SingleAsync<Message>(m => m.Id == sent.Id);
        Assert.Equal(peerProfile, persisted.RecipientProfileId);

        var expectedKey = $"message_{CommunityTestData.ConversationKey(meProfile, peerProfile)}";
        var evt = Assert.Single(sender.Sent);
        Assert.Equal(expectedKey, evt.Topic);
        Assert.Equal(sent.Id, Assert.IsType<Message>(evt.Message).Id);
    }
}
