using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>El round-trip se ejecuta exclusivamente en la BD desechable de la colección.</summary>
[Collection(CommunityTestCollection.Name)]
public sealed class ProfileBlockingMigrationTests(CommunityTestDatabase fixture)
{
    [Fact]
    public async Task BlockingMigration_DownThenUp_RestoresFunctionAndTable_AndPreservesExistingEvidence()
    {
        if (!fixture.Available)
            throw new InvalidOperationException("COP_TEST_DB_CONNECTION local is required for blocking regressions.");
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        Assert.StartsWith("coppaddresd_community_test_", fixture.DatabaseName);
        Assert.Equal(fixture.DatabaseName, connection.Database);
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var factory = new CommunityTestContext(fixture.ConnectionString);
        Guid actorUser = Guid.NewGuid();
        Guid peerProfile;
        var postId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        const string evidence = "synthetic migration round-trip evidence";
        await using (var seed = factory.Create())
        {
            var actor = await CommunityTestData.SeedProfileAsync(seed, actorUser, "synthetic migration actor");
            var peer = await CommunityTestData.SeedProfileAsync(seed, Guid.NewGuid(), "synthetic migration peer");
            peerProfile = peer.Id;
            seed.Posts.Add(new Post { Id = postId, ProfileId = peer.Id, Body = evidence });
            seed.Comments.Add(new Comment { Id = commentId, PostId = postId, ProfileId = peer.Id, Body = evidence });
            seed.Messages.Add(new Message { Id = messageId, SenderProfileId = peer.Id, RecipientProfileId = actor.Id, Body = evidence, CreatedAt = DateTime.UtcNow });
            seed.PostReports.Add(new PostReport { Id = reportId, PostId = postId, ReportedByProfileId = actor.Id, Reason = evidence });
            await seed.SaveChangesAsync();
        }
        await using (var migrationContext = factory.Create())
        {
            var migrator = migrationContext.GetService<IMigrator>();
            try
            {
                await migrator.MigrateAsync("20260917164102_RenameAntaresBrandInCommunityData");
                await AssertBlockingSchemaAsync(present: false);
            }
            finally
            {
                // Incluso un assert fallido deja el esquema actual para los demás tests.
                await migrator.MigrateAsync("20261110180222_AddCommunityProfileBlocks");
            }
        }
        await AssertBlockingSchemaAsync(present: true);
        await using (var check = factory.Create())
        {
            Assert.Contains("20261110180222_AddCommunityProfileBlocks", await check.Database.GetAppliedMigrationsAsync());
            Assert.Equal(evidence, (await check.Posts.SingleAsync(p => p.Id == postId)).Body);
            Assert.Equal(evidence, (await check.Comments.SingleAsync(c => c.Id == commentId)).Body);
            Assert.Equal(evidence, (await check.Messages.SingleAsync(m => m.Id == messageId)).Body);
            Assert.Equal(evidence, (await check.PostReports.SingleAsync(r => r.Id == reportId)).Reason);
        }
        await using var actorClient = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, actorUser);
        await actorClient.BlockAsync(peerProfile);
        var hidden = await actorClient.ExecuteAsync($$"""{ post(id: "{{postId}}") { id body } }""");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, hidden.GetProperty("data").GetProperty("post").ValueKind);
    }

    private async Task AssertBlockingSchemaAsync(bool present)
    {
        await using var probe = new NpgsqlConnection(fixture.ConnectionString);
        await probe.OpenAsync();
        await using var command = probe.CreateCommand();
        command.CommandText = """
            SELECT (to_regclass('community.profile_blocks') IS NOT NULL) = @present
               AND (to_regprocedure('community.is_comment_thread_visible(uuid,uuid)') IS NOT NULL) = @present
            """;
        command.Parameters.AddWithValue("present", present);
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }
}
