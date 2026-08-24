using CoppAddresd.Community.GraphQL.Subscriptions;
using CoppAddresd.Community.Persistence;
using HotChocolate;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests del control de acceso a la suscripción MessageAdded: solo los participantes
/// de la conversación (o el usuario con perfil) pueden suscribirse a un tópico.
/// Cada test usa usuarios únicos porque la BD de la colección es compartida.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class SubscriptionTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => Npgsql.NpgsqlConnection.ClearAllPools();

    [Theory]
    [InlineData("aaaa:bbbb", false)]
    [InlineData("", false)]
    [InlineData("not-a-guid:bbbb", false)]
    [InlineData("aaaa", false)]
    public void TryParseConversationKey_ValidatesFormat(string key, bool expected)
    {
        var ok = CommunitySubscription.TryParseConversationKey(key, out var a, out var b);
        Assert.Equal(expected, ok);
    }

    [Fact]
    public void TryParseConversationKey_ParsesTwoGuids()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var ok = CommunitySubscription.TryParseConversationKey($"{a}:{b}", out var pa, out var pb);

        Assert.True(ok);
        Assert.Equal(a, pa);
        Assert.Equal(b, pb);
    }

    [Fact]
    public async Task EnsureParticipant_AllowsEitherSideOfTheConversation()
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var peerUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana");
        var peer = await CommunityTestData.SeedProfileAsync(db, peerUser, "Bruno");
        var key = CommunityTestData.ConversationKey(me.Id, peer.Id);

        var mine = await CommunitySubscription.EnsureParticipantAsync(
            key, db, CommunityTestData.HttpAs(meUser), CancellationToken.None);
        var peerSide = await CommunitySubscription.EnsureParticipantAsync(
            key, db, CommunityTestData.HttpAs(peerUser), CancellationToken.None);

        Assert.Equal(me.Id, mine);
        Assert.Equal(peer.Id, peerSide);
    }

    [Fact]
    public async Task EnsureParticipant_RejectsOutsider()
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var peerUser = Guid.NewGuid();
        var outsiderUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana");
        var peer = await CommunityTestData.SeedProfileAsync(db, peerUser, "Bruno");
        await CommunityTestData.SeedProfileAsync(db, outsiderUser, "Carla");
        var key = CommunityTestData.ConversationKey(me.Id, peer.Id);

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            CommunitySubscription.EnsureParticipantAsync(
                key, db, CommunityTestData.HttpAs(outsiderUser), CancellationToken.None));

        Assert.Contains("No puedes suscribirte", ex.Message);
    }

    [Fact]
    public async Task EnsureParticipant_RejectsMalformedKey()
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var peerUser = Guid.NewGuid();
        await CommunityTestData.SeedProfileAsync(db, meUser, "Ana");
        await CommunityTestData.SeedProfileAsync(db, peerUser, "Bruno");

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            CommunitySubscription.EnsureParticipantAsync(
                "not-a-valid-key", db, CommunityTestData.HttpAs(meUser), CancellationToken.None));

        Assert.Contains("No puedes suscribirte", ex.Message);
    }

    [Fact]
    public async Task EnsureParticipant_RejectsUserWithoutProfile()
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var peerUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana");
        var peer = await CommunityTestData.SeedProfileAsync(db, peerUser, "Bruno");
        var key = CommunityTestData.ConversationKey(me.Id, peer.Id);

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            CommunitySubscription.EnsureParticipantAsync(
                key, db, CommunityTestData.HttpAs(Guid.NewGuid()), CancellationToken.None));

        Assert.Contains("perfil", ex.Message);
    }

    [Fact]
    public async Task EnsureParticipant_RejectsUnauthenticated()
    {
        await using var db = _factory.Create();
        var meUser = Guid.NewGuid();
        var peerUser = Guid.NewGuid();
        var me = await CommunityTestData.SeedProfileAsync(db, meUser, "Ana");
        var peer = await CommunityTestData.SeedProfileAsync(db, peerUser, "Bruno");
        var key = CommunityTestData.ConversationKey(me.Id, peer.Id);

        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            CommunitySubscription.EnsureParticipantAsync(
                key, db, http, CancellationToken.None));

        Assert.Contains("autenticado", ex.Message);
    }
}
