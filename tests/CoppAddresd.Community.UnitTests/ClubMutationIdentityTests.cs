using System.Security.Claims;
using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Identidad JWT en las mutaciones de clubes auditadas (Fase 10):
/// joinClub (JoinClubDirect), leaveClub (LeaveClub), likePost
/// (ToggleClubPostLike), votePoll (VoteClubPoll), createPost (CreatePost) y
/// reportPost (ReportPost) resuelven al actor desde el JWT y nunca aceptan un
/// userId/perfil del cliente.
/// </summary>
public sealed class ClubMutationIdentityTests
{
    private static CommunityDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new CommunityDbContext(options);
    }

    private static IHttpContextAccessor HttpWithUser(Guid? userId, string? name = null)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            if (name is not null)
                claims.Add(new Claim(ClaimTypes.Name, name));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var http = Substitute.For<IHttpContextAccessor>();
        http.HttpContext.Returns(new DefaultHttpContext { User = principal });
        return http;
    }

    private static ITopicEventSender FakeSender() => Substitute.For<ITopicEventSender>();

    private static Profile MakeProfile(Guid userId, string name = "Miembro Test") =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = name,
            Status = ProfileStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };

    private static Club MakeClub(ClubVisibility visibility = ClubVisibility.Publico) =>
        new()
        {
            Id = Guid.NewGuid(),
            Slug = $"club-{Guid.NewGuid():N}",
            Name = "Club Test",
            Description = "Club de prueba",
            Category = "Salud",
            Visibility = visibility,
            CreatedByProfileId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

    private static async Task<ClubMember> AddMemberAsync(
        CommunityDbContext db,
        Guid clubId,
        Guid profileId,
        ClubMemberRole role = ClubMemberRole.Miembro,
        ClubMemberStatus status = ClubMemberStatus.Activo
    )
    {
        var member = new ClubMember
        {
            ClubId = clubId,
            ProfileId = profileId,
            Role = role,
            Status = status,
            JoinedAt = DateTime.UtcNow,
        };
        db.ClubMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    [Fact]
    public void ClubMutation_ExigeAutorizacionANivelDeClase()
    {
        var attrs = typeof(ClubMutation).GetCustomAttributes(
            typeof(AuthorizeAttribute),
            inherit: true
        );
        Assert.NotEmpty(attrs);
    }

    [Fact]
    public void CommunityMutation_ExigeAutorizacionANivelDeClase()
    {
        var attrs = typeof(CommunityMutation).GetCustomAttributes(
            typeof(AuthorizeAttribute),
            inherit: true
        );
        Assert.NotEmpty(attrs);
    }

    [Fact]
    public async Task JoinClubDirect_AtribuyeMembresiaAlPerfilDelJwt()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var profile = MakeProfile(userId);
        var club = MakeClub();
        db.Profiles.Add(profile);
        db.Clubs.Add(club);
        await db.SaveChangesAsync();

        var mutation = new ClubMutation();
        var member = await mutation.JoinClubDirect(
            club.Id,
            db,
            HttpWithUser(userId),
            FakeSender(),
            CancellationToken.None
        );

        Assert.Equal(club.Id, member.ClubId);
        Assert.Equal(profile.Id, member.ProfileId);
        Assert.Equal(ClubMemberStatus.Activo, member.Status);
        Assert.Equal(1, await db.ClubMembers.CountAsync());
    }

    [Fact]
    public async Task JoinClubDirect_SinJwt_LanzaYNoAprovisionaNada()
    {
        using var db = CreateInMemoryDb();
        var club = MakeClub();
        db.Clubs.Add(club);
        await db.SaveChangesAsync();

        var mutation = new ClubMutation();
        await Assert.ThrowsAsync<GraphQLException>(() =>
            mutation.JoinClubDirect(
                club.Id,
                db,
                HttpWithUser(null),
                FakeSender(),
                CancellationToken.None
            )
        );

        // Blindaje: ningún perfil huérfano (UserId nulo) y ninguna membresía.
        Assert.Equal(0, await db.Profiles.CountAsync(p => p.UserId == null && !p.IsSystem));
        Assert.Equal(0, await db.ClubMembers.CountAsync());
    }

    [Fact]
    public async Task JoinClubDirect_AutoaprovisionaSoloAlLlamanteSinTocarAOtros()
    {
        using var db = CreateInMemoryDb();
        var userA = Guid.NewGuid();
        var profileA = MakeProfile(userA, "Miembro A");
        var club = MakeClub();
        db.Profiles.Add(profileA);
        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        await AddMemberAsync(db, club.Id, profileA.Id);

        // B no tiene perfil: se autoaprovisiona con SU userId, sin tocar a A.
        var userB = Guid.NewGuid();
        var mutation = new ClubMutation();
        var memberB = await mutation.JoinClubDirect(
            club.Id,
            db,
            HttpWithUser(userB, "Miembro B"),
            FakeSender(),
            CancellationToken.None
        );

        var profileB = await db.Profiles.SingleAsync(p => p.UserId == userB);
        Assert.Equal(profileB.Id, memberB.ProfileId);
        Assert.NotEqual(profileA.Id, memberB.ProfileId);
        Assert.Equal(2, await db.ClubMembers.CountAsync());
        Assert.True(
            await db.ClubMembers.AnyAsync(m =>
                m.ClubId == club.Id
                && m.ProfileId == profileA.Id
                && m.Status == ClubMemberStatus.Activo
            )
        );
    }

    [Fact]
    public async Task LeaveClub_SoloEliminaLaMembresiaDelLlamante()
    {
        using var db = CreateInMemoryDb();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var profileA = MakeProfile(userA, "Miembro A");
        var profileB = MakeProfile(userB, "Miembro B");
        var club = MakeClub();
        db.Profiles.AddRange(profileA, profileB);
        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        await AddMemberAsync(db, club.Id, profileA.Id);
        await AddMemberAsync(db, club.Id, profileB.Id);

        var mutation = new ClubMutation();
        Assert.True(
            await mutation.LeaveClub(club.Id, db, HttpWithUser(userB), CancellationToken.None)
        );

        Assert.False(
            await db.ClubMembers.AnyAsync(m => m.ClubId == club.Id && m.ProfileId == profileB.Id)
        );
        Assert.True(
            await db.ClubMembers.AnyAsync(m => m.ClubId == club.Id && m.ProfileId == profileA.Id)
        );
    }

    [Fact]
    public async Task ToggleClubPostLike_AtribuyeLikeAlPerfilDelLlamante()
    {
        using var db = CreateInMemoryDb();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var profileA = MakeProfile(userA, "Miembro A");
        var profileB = MakeProfile(userB, "Miembro B");
        var club = MakeClub();
        db.Profiles.AddRange(profileA, profileB);
        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        await AddMemberAsync(db, club.Id, profileA.Id);
        await AddMemberAsync(db, club.Id, profileB.Id);
        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = profileB.Id,
            ClubId = club.Id,
            Body = "Post del club",
            Type = PostType.Texto,
            CreatedAt = DateTime.UtcNow,
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var mutation = new ClubMutation();
        await mutation.ToggleClubPostLike(
            post.Id,
            db,
            HttpWithUser(userA),
            FakeSender(),
            CancellationToken.None
        );

        Assert.True(
            await db.Likes.AnyAsync(l => l.PostId == post.Id && l.ProfileId == profileA.Id)
        );
        Assert.False(
            await db.Likes.AnyAsync(l => l.PostId == post.Id && l.ProfileId == profileB.Id)
        );
    }

    [Fact]
    public async Task VoteClubPoll_VotoQuedaEnElPerfilDelLlamante()
    {
        using var db = CreateInMemoryDb();
        var userA = Guid.NewGuid();
        var profileA = MakeProfile(userA);
        var club = MakeClub();
        db.Profiles.Add(profileA);
        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        await AddMemberAsync(db, club.Id, profileA.Id);
        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = profileA.Id,
            ClubId = club.Id,
            Body = "Encuesta del club",
            Type = PostType.Encuesta,
            CreatedAt = DateTime.UtcNow,
        };
        var poll = new Poll
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            CreatedAt = DateTime.UtcNow,
        };
        var option1 = new PollOption
        {
            Id = Guid.NewGuid(),
            PollId = poll.Id,
            Text = "Opción 1",
            Position = 0,
        };
        var option2 = new PollOption
        {
            Id = Guid.NewGuid(),
            PollId = poll.Id,
            Text = "Opción 2",
            Position = 1,
        };
        db.Posts.Add(post);
        db.Polls.Add(poll);
        db.PollOptions.AddRange(option1, option2);
        await db.SaveChangesAsync();

        var mutation = new ClubMutation();
        await mutation.VoteClubPoll(
            post.Id,
            option2.Id,
            db,
            HttpWithUser(userA),
            CancellationToken.None
        );

        var vote = Assert.Single(await db.PollVotes.ToListAsync());
        Assert.Equal(option2.Id, vote.OptionId);
        Assert.Equal(profileA.Id, vote.ProfileId);
    }

    [Fact]
    public async Task CreatePost_AtribuyePostAlPerfilDelJwt()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var profile = MakeProfile(userId);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();

        var mutation = new CommunityMutation();
        var post = await mutation.CreatePost(
            "Post desde el JWT",
            PostType.Texto,
            null,
            db,
            HttpWithUser(userId),
            Substitute.For<ITopicEventSender>(),
            imageKey: null,
            ct: CancellationToken.None
        );

        Assert.Equal(profile.Id, post.ProfileId);
        Assert.Equal("Post desde el JWT", post.Body);
    }

    [Fact]
    public async Task ReportPost_AtribuyeReporteAlPerfilDelJwt()
    {
        using var db = CreateInMemoryDb();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var profileA = MakeProfile(userA, "Miembro A");
        var profileB = MakeProfile(userB, "Miembro B");
        db.Profiles.AddRange(profileA, profileB);
        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = profileB.Id,
            Body = "Post reportable",
            Type = PostType.Texto,
            CreatedAt = DateTime.UtcNow,
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var mutation = new CommunityMutation();
        var report = await mutation.ReportPost(
            post.Id,
            "Spam",
            null,
            db,
            HttpWithUser(userA),
            CancellationToken.None
        );

        Assert.Equal(post.Id, report.PostId);
        Assert.Equal(profileA.Id, report.ReportedByProfileId);
        Assert.NotEqual(profileB.Id, report.ReportedByProfileId);
    }
}
