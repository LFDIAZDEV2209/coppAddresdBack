using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests del flujo de encuestas (mutation CreatePollPost + VotePoll): reglas de
/// negocio de creación (2-4 opciones, texto no vacío), un voto por perfil por
/// encuesta y la publicación del evento post_added. La BD de la colección es
/// compartida: cada test usa usuarios únicos.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class PollTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => Npgsql.NpgsqlConnection.ClearAllPools();

    private async Task<Guid> SeedActiveUserAsync(CancellationToken ct = default)
    {
        var profile = await CommunityTestData.SeedProfileAsync(
            _factory.Create(), Guid.NewGuid(), "Pollster", ct);
        return profile.UserId;
    }

    [Fact]
    public async Task CreatePollPost_CreatesPostWithPollAndOptions()
    {
        var userId = await SeedActiveUserAsync();
        await using var db = _factory.Create();

        var post = await new CommunityMutation().CreatePollPost(
            "¿Cuál es tu hábito favorito?",
            ["Correr", "Meditar", "Comer sano"],
            db, CommunityTestData.HttpAs(userId), new RecordingTopicEventSender(),
            CancellationToken.None);

        var loaded = await db.Posts
            .Include(p => p.Poll!).ThenInclude(p => p.Options)
            .FirstAsync(p => p.Id == post.Id);
        Assert.Equal("¿Cuál es tu hábito favorito?", loaded.Body);
        Assert.NotNull(loaded.Poll);
        Assert.Equal(3, loaded.Poll.Options.Count);
        Assert.Equal(["Correr", "Meditar", "Comer sano"],
            loaded.Poll.Options.OrderBy(o => o.Position).Select(o => o.Text).ToArray());
    }

    [Fact]
    public async Task CreatePollPost_Rejects_TooFewOptions()
    {
        var userId = await SeedActiveUserAsync();
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            new CommunityMutation().CreatePollPost(
                "¿Pregunta?", ["Única opción"], db, CommunityTestData.HttpAs(userId),
                new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("2 y 4", ex.Message);
    }

    [Fact]
    public async Task CreatePollPost_Rejects_DuplicateOptions()
    {
        var userId = await SeedActiveUserAsync();
        await using var db = _factory.Create();

        // Duplicados con distinto casing: se normalizan y quedan 2.
        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            new CommunityMutation().CreatePollPost(
                "¿Pregunta?", ["A", "A", "a", "B"], db, CommunityTestData.HttpAs(userId),
                new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("2 y 4", ex.Message);
    }

    [Fact]
    public async Task CreatePollPost_Rejects_EmptyQuestion()
    {
        var userId = await SeedActiveUserAsync();
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            new CommunityMutation().CreatePollPost(
                "   ", ["A", "B"], db, CommunityTestData.HttpAs(userId),
                new RecordingTopicEventSender(), CancellationToken.None));

        Assert.Contains("pregunta", ex.Message);
    }

    [Fact]
    public async Task VotePoll_RegistersVoteAndReturnsResults()
    {
        var userId = await SeedActiveUserAsync();
        await using var db = _factory.Create();

        var poll = await new CommunityMutation().CreatePollPost(
            "¿Pregunta?", ["A", "B"], db, CommunityTestData.HttpAs(userId),
            new RecordingTopicEventSender(), CancellationToken.None);
        var pollId = poll.Poll!.Options.First().Id;

        var post = await new CommunityMutation().VotePoll(
            pollId, db, CommunityTestData.HttpAs(userId), CancellationToken.None);

        Assert.NotNull(post.Poll);
        var voted = post.Poll.Options.First(o => o.Id == pollId);
        Assert.Single(voted.Votes);
        var total = post.Poll.Options.Sum(o => o.Votes.Count);
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task VotePoll_Rejects_DoubleVote()
    {
        var userId = await SeedActiveUserAsync();
        await using var db = _factory.Create();

        var poll = await new CommunityMutation().CreatePollPost(
            "¿Pregunta?", ["A", "B"], db, CommunityTestData.HttpAs(userId),
            new RecordingTopicEventSender(), CancellationToken.None);
        var optionA = poll.Poll!.Options.First(o => o.Position == 0).Id;
        var optionB = poll.Poll!.Options.First(o => o.Position == 1).Id;

        await new CommunityMutation().VotePoll(
            optionA, db, CommunityTestData.HttpAs(userId), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            new CommunityMutation().VotePoll(
                optionB, db, CommunityTestData.HttpAs(userId), CancellationToken.None));

        Assert.Contains("Ya votaste", ex.Message);
    }

    [Fact]
    public async Task VotePoll_Rejects_UnknownOption()
    {
        var userId = await SeedActiveUserAsync();
        await using var db = _factory.Create();

        var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
            new CommunityMutation().VotePoll(
                Guid.NewGuid(), db, CommunityTestData.HttpAs(userId), CancellationToken.None));

        Assert.Contains("No se encontró", ex.Message);
    }
}
