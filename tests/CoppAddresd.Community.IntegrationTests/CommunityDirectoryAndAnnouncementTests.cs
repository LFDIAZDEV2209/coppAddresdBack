using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

[Collection(CommunityTestCollection.Name)]
public sealed class CommunityDirectoryAndAnnouncementTests(CommunityTestDatabase fixture)
{
    [Fact]
    public async Task DirectorySearchesBeyondFirstFiftyAndCountsFilteredPages()
    {
        if (!fixture.Available) throw new InvalidOperationException("COP_TEST_DB_CONNECTION is required for this regression.");
        await using var db = new CommunityTestContext(fixture.ConnectionString).Create();
        var prefix = $"qa-directory-{Guid.NewGuid():N}";
        var rows = Enumerable.Range(0, 65).Select(i => new Profile {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DisplayName = $"{prefix} Miembro {i:D2}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i), Diagnosis = ProfileDiagnosis.DM2HTA,
            Region = ProfileRegion.NY, Status = ProfileStatus.Active,
        }).ToArray();
        rows[64].DisplayName = $"{prefix} Luís Prueba Movil";
        db.Profiles.AddRange(rows);
        await db.SaveChangesAsync();
        var query = new CommunityQuery();
        var http = CommunityTestData.HttpAs(Guid.NewGuid());
        var match = await query.ProfilesPage(db, http, CancellationToken.None, search: $"{prefix} Luis Prueba", take: 5);
        Assert.Equal(1, match.TotalCount);
        Assert.Equal(rows[64].Id, Assert.Single(match.Items).Id);
        var page = await query.ProfilesPage(db, http, CancellationToken.None, search: prefix,
            diagnosis: ProfileDiagnosis.DM2HTA, region: ProfileRegion.NY, take: 5, skip: 60);
        Assert.Equal(65, page.TotalCount);
        Assert.Equal(rows.Skip(60).Select(row => row.Id), page.Items.Select(row => row.Id));
    }

    [Fact]
    public async Task OfficialAnnouncementPollPersistsOptionsAndAcceptsPatientVote()
    {
        if (!fixture.Available) throw new InvalidOperationException("COP_TEST_DB_CONNECTION is required for this regression.");
        await using var db = new CommunityTestContext(fixture.ConnectionString).Create();
        var user = Guid.NewGuid();
        await CommunityTestData.SeedProfileAsync(db, user, "QA patient");
        var mutations = new CommunityMutation();
        var announcement = await mutations.CreateAnnouncement("¿Flujo QA?", PostType.Encuesta,
            PostDestination.ComunidadADRED, db, new RecordingTopicEventSender(), CancellationToken.None,
            pollOptions: ["ERP", "App"]);
        db.ChangeTracker.Clear();
        var persisted = await db.Posts.Include(p => p.Profile).Include(p => p.Poll!).ThenInclude(p => p.Options)
            .SingleAsync(p => p.Id == announcement.Id);
        Assert.True(persisted.Profile!.IsSystem);
        Assert.Equal(PostDestination.ComunidadADRED, persisted.Destination);
        Assert.Equal(["ERP", "App"], persisted.Poll!.Options.OrderBy(o => o.Position).Select(o => o.Text).ToArray());
        var option = persisted.Poll.Options.First();
        var voted = await mutations.VotePoll(option.Id, db, CommunityTestData.HttpAs(user), CancellationToken.None);
        Assert.Single(voted.Poll!.Options.Single(o => o.Id == option.Id).Votes);
    }
}
