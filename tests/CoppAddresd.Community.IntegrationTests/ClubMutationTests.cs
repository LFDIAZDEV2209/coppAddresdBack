using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Types;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests de las reglas de negocio del módulo de clubes: membresía por visibilidad,
/// cupos con anti-overbooking atómico, visibilidad del feed, voto único en encuestas,
/// like toggle, silencio/expulsión y moderación. Requieren PostgreSQL
/// (COP_TEST_DB_CONNECTION); se omiten si no está disponible.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class ClubMutationTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => NpgsqlConnection.ClearAllPools();

    private async Task<CommunityDbContext> NewDbAsync()
    {
        var db = _factory.Create();
        await using var _ = db;
        return db;
    }

    /// <summary>Crea un club con el perfil dado como Admin (por siembra directa).</summary>
    private static async Task<Club> SeedClubAsync(
        CommunityDbContext db, Profile admin, string slug, ClubVisibility visibility, int? maxMembers = null, CancellationToken ct = default)
    {
        var club = new Club
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = slug,
            Description = "Club de prueba",
            Rules = [],
            Objectives = [],
            Category = "Salud",
            Tags = [],
            Visibility = visibility,
            MaxMembers = maxMembers,
            CreatedByProfileId = admin.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.Clubs.Add(club);
        db.ClubMembers.Add(new ClubMember
        {
            ClubId = club.Id,
            ProfileId = admin.Id,
            Role = ClubMemberRole.Admin,
            Status = ClubMemberStatus.Activo,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return club;
    }

    private static async Task<Post> SeedClubPostAsync(
        CommunityDbContext db, Club club, Profile author, string body,
        ClubPostVisibility visibility = ClubPostVisibility.Publico, CancellationToken ct = default)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = author.Id,
            ClubId = club.Id,
            Body = body,
            ClubVisibility = visibility,
            ClubStatus = ClubPostStatus.Publicado,
            CreatedAt = DateTime.UtcNow,
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        return post;
    }

    [Fact]
    public async Task JoinClubDirect_ClubPublico_CreaMembresiaActiva()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "pub-1", ClubVisibility.Publico);

        var member = await new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);

        Assert.Equal(ClubMemberStatus.Activo, member.Status);
        Assert.Equal(ClubMemberRole.Miembro, member.Role);
        Assert.Single(await db.ClubMembers.Where(m => m.ClubId == club.Id).ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task JoinClubDirect_CupoLleno_LanzaError()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        // Cupo 1: ya lo ocupa el admin.
        var club = await SeedClubAsync(db, admin, "full-1", ClubVisibility.Publico, maxMembers: 1);

        await Assert.ThrowsAsync<GraphQLException>(() =>
            new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None));
    }

    [Fact]
    public async Task RequestMembership_Privado_QuedaPendiente_Y_ApruebaConNotificacion()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "priv-1", ClubVisibility.Privado);

        var requested = await new ClubMutation().RequestMembership(club.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);
        Assert.Equal(ClubMemberStatus.Pendiente, requested.Status);

        var sender = new RecordingTopicEventSender();
        var approved = await new ClubMutation().ApproveMembership(club.Id, user.Id, db, CommunityTestData.HttpAs(admin.UserId!.Value), sender, CancellationToken.None);
        Assert.Equal(ClubMemberStatus.Activo, approved.Status);
        Assert.Contains(sender.Sent, s => s.Topic == $"club_member_{user.Id}");
        Assert.Contains(await db.ClubNotifications.Where(n => n.ProfileId == user.Id).Select(n => n.Type).ToListAsync(CancellationToken.None), t => t == ClubNotificationType.SolicitudAprobada);
    }

    [Fact]
    public async Task JoinWithInvitation_TokenValido_Une_Y_Consume()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "inv-1", ClubVisibility.Invitacion);

        var invitation = await new ClubMutation().CreateInvitation(club.Id, null, DateTime.UtcNow.AddDays(1), db, CommunityTestData.HttpAs(admin.UserId!.Value), CancellationToken.None);

        var member = await new ClubMutation().JoinWithInvitation(invitation.Token, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);
        Assert.Equal(ClubMemberStatus.Activo, member.Status);

        // Segundo uso: token ya consumido.
        await Assert.ThrowsAsync<GraphQLException>(() =>
            new ClubMutation().JoinWithInvitation(invitation.Token, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None));
    }

    [Fact]
    public async Task ClubFeed_NoMiembro_SoloVePublicos()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var outsider = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Outsider", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "feed-1", ClubVisibility.Publico);
        await SeedClubPostAsync(db, club, admin, "Público");
        await SeedClubPostAsync(db, club, admin, "Privado", ClubPostVisibility.Privado);

        var feed = await new ClubQuery().ClubFeed(club.Id, db, CommunityTestData.HttpAs(outsider.UserId!.Value), 20, 0, CancellationToken.None);

        Assert.Single(feed);
        Assert.Equal("Público", feed[0].Body);
    }

    [Fact]
    public async Task VoteClubPoll_UnVotoPorPerfil_Reemplaza()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "poll-1", ClubVisibility.Publico);
        await new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);

        var post = await SeedClubPostAsync(db, club, admin, "¿Mañana o tarde?");
        var poll = new Poll { Id = Guid.NewGuid(), PostId = post.Id, CreatedAt = DateTime.UtcNow };
        var optA = new PollOption { Id = Guid.NewGuid(), PollId = poll.Id, Text = "Mañana", Position = 0 };
        var optB = new PollOption { Id = Guid.NewGuid(), PollId = poll.Id, Text = "Tarde", Position = 1 };
        db.Polls.Add(poll);
        db.PollOptions.AddRange(optA, optB);
        await db.SaveChangesAsync(CancellationToken.None);

        await new ClubMutation().VoteClubPoll(post.Id, optA.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), CancellationToken.None);
        await new ClubMutation().VoteClubPoll(post.Id, optB.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), CancellationToken.None);

        Assert.Single(await db.PollVotes.Where(v => v.ProfileId == user.Id).ToListAsync(CancellationToken.None));
        Assert.Equal(optB.Id, (await db.PollVotes.SingleAsync(v => v.ProfileId == user.Id, CancellationToken.None)).OptionId);
    }

    [Fact]
    public async Task ToggleClubPostLike_Alterna()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "like-1", ClubVisibility.Publico);
        await new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);
        var post = await SeedClubPostAsync(db, club, admin, "Hola club");

        await new ClubMutation().ToggleClubPostLike(post.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);
        Assert.Single(await db.Likes.Where(l => l.PostId == post.Id).ToListAsync(CancellationToken.None));

        await new ClubMutation().ToggleClubPostLike(post.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);
        Assert.Empty(await db.Likes.Where(l => l.PostId == post.Id).ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AddClubComment_NoMiembro_Lanza()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var outsider = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Outsider", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "cmt-1", ClubVisibility.Publico);
        var post = await SeedClubPostAsync(db, club, admin, "Post");

        await Assert.ThrowsAsync<GraphQLException>(() =>
            new ClubMutation().AddClubComment(post.Id, "Hola", db, CommunityTestData.HttpAs(outsider.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None));
    }

    [Fact]
    public async Task MuteMember_NoPuedeComentar()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "mute-1", ClubVisibility.Publico);
        await new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);
        var post = await SeedClubPostAsync(db, club, admin, "Post");

        await new ClubMutation().MuteMember(club.Id, user.Id, DateTime.UtcNow.AddHours(1), db, CommunityTestData.HttpAs(admin.UserId!.Value), CancellationToken.None);

        await Assert.ThrowsAsync<GraphQLException>(() =>
            new ClubMutation().AddClubComment(post.Id, "Silenciado", db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmAttendance_AntiOverbooking_SegundoVaAListaEspera()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var u1 = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "U1", CancellationToken.None);
        var u2 = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "U2", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "ev-1", ClubVisibility.Publico);
        await new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(u1.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);
        await new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(u2.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);

        var @event = new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Evento cupo 1",
            Description = "",
            Type = ClubEventType.Virtual,
            StartsAt = DateTime.UtcNow.AddDays(1),
            EndsAt = DateTime.UtcNow.AddDays(1).AddHours(1),
            MaxAttendees = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.ClubEvents.Add(@event);
        await db.SaveChangesAsync(CancellationToken.None);

        var first = await new ClubMutation().ConfirmAttendance(@event.Id, db, CommunityTestData.HttpAs(u1.UserId!.Value), CancellationToken.None);
        var second = await new ClubMutation().ConfirmAttendance(@event.Id, db, CommunityTestData.HttpAs(u2.UserId!.Value), CancellationToken.None);

        Assert.Equal(EventAttendanceStatus.Confirmado, first.Status);
        Assert.Equal(EventAttendanceStatus.ListaEspera, second.Status);
        Assert.Equal(1, (await db.ClubEvents.SingleAsync(e => e.Id == @event.Id, CancellationToken.None)).ConfirmedCount);
        Assert.Equal(1, (await db.ClubEvents.SingleAsync(e => e.Id == @event.Id, CancellationToken.None)).WaitlistCount);
    }

    [Fact]
    public async Task CheckIn_RequiereConfirmacion_Previa()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var user = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "User", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "chk-1", ClubVisibility.Publico);
        await new ClubMutation().JoinClubDirect(club.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), new RecordingTopicEventSender(), CancellationToken.None);

        var @event = new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Evento check-in",
            Description = "",
            Type = ClubEventType.Presencial,
            StartsAt = DateTime.UtcNow.AddDays(1),
            EndsAt = DateTime.UtcNow.AddDays(1).AddHours(1),
            CreatedAt = DateTime.UtcNow,
        };
        db.ClubEvents.Add(@event);
        await db.SaveChangesAsync(CancellationToken.None);

        // Sin confirmación previa → error.
        await Assert.ThrowsAsync<GraphQLException>(() =>
            new ClubMutation().CheckIn(@event.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), CancellationToken.None));

        await new ClubMutation().ConfirmAttendance(@event.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), CancellationToken.None);
        var checkedIn = await new ClubMutation().CheckIn(@event.Id, db, CommunityTestData.HttpAs(user.UserId!.Value), CancellationToken.None);
        Assert.Equal(EventAttendanceStatus.CheckIn, checkedIn.Status);
    }

    [Fact]
    public async Task ResolveReport_Resuelto_BorraElPostDelClub()
    {
        var db = _factory.Create();
        await using var _ = db;
        var admin = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Admin", CancellationToken.None);
        var reporter = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "Reporter", CancellationToken.None);
        var club = await SeedClubAsync(db, admin, "rep-1", ClubVisibility.Publico);
        var post = await SeedClubPostAsync(db, club, admin, "Contenido reportado");

        var report = new PostReport
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            ReportedByProfileId = reporter.Id,
            Reason = "Contenido inapropiado",
            CreatedAt = DateTime.UtcNow,
        };
        db.PostReports.Add(report);
        await db.SaveChangesAsync(CancellationToken.None);

        var resolved = await new ClubMutation().ResolveReport(club.Id, report.Id, "RESUELTO", db, CommunityTestData.HttpAs(admin.UserId!.Value), CancellationToken.None);
        Assert.True(resolved);
        Assert.NotNull((await db.Posts.SingleAsync(p => p.Id == post.Id, CancellationToken.None)).DeletedAt);
    }
}