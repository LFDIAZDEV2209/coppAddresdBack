using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests del ClubSeeder: contenido sembrado (clubes de sistema + demo, categorías,
/// miembros, posts, eventos, lives, solicitudes) e idempotencia. Requieren PostgreSQL
/// (COP_TEST_DB_CONNECTION); se omiten si no está disponible.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class ClubSeederTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private const string CaminantesSlug = "caminantes-adres";

    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => NpgsqlConnection.ClearAllPools();

    private async Task<CommunityDbContext> CreateSeededDbAsync()
    {
        var db = _factory.Create();

        // El ClubSeeder exige el perfil de sistema (lo crea CommunitySeeder en arranque).
        if (!await db.Profiles.AnyAsync(p => p.IsSystem, CancellationToken.None))
        {
            db.Profiles.Add(new Profile
            {
                Id = Guid.NewGuid(),
                UserId = null,
                IsSystem = true,
                DisplayName = "Equipo ANTARES",
                Status = ProfileStatus.Active,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await ClubSeeder.SeedAsync(db, null, CancellationToken.None);
        return db;
    }

    [Fact]
    public async Task Seed_CreaClubesSistemaYDemo_ConContenido()
    {
        var db = await CreateSeededDbAsync();
        await using var _ = db;

        // 5 clubes de sistema (antiguos PostDestination) + 9 demo.
        Assert.Equal(14, await db.Clubs.CountAsync(CancellationToken.None));
        Assert.Equal(5, await db.Clubs.CountAsync(c => c.IsSystem, CancellationToken.None));
        Assert.Equal(9, await db.Clubs.CountAsync(c => !c.IsSystem, CancellationToken.None));

        // Catálogo de categorías completo.
        Assert.Equal(9, await db.ClubCategories.CountAsync(CancellationToken.None));

        // Caminantes ADRES: miembros (11 activos + 1 solicitud pendiente), posts, eventos, live.
        var caminantes = await db.Clubs.SingleAsync(c => c.Slug == CaminantesSlug, CancellationToken.None);
        Assert.Equal(ClubVisibility.Publico, caminantes.Visibility);
        Assert.Equal(11, await db.ClubMembers.CountAsync(m => m.ClubId == caminantes.Id && m.Status == ClubMemberStatus.Activo, CancellationToken.None));
        Assert.Equal(1, await db.ClubMembers.CountAsync(m => m.ClubId == caminantes.Id && m.Status == ClubMemberStatus.Pendiente, CancellationToken.None));
        Assert.Equal(5, await db.Posts.CountAsync(p => p.ClubId == caminantes.Id, CancellationToken.None));
        Assert.Equal(2, await db.ClubEvents.CountAsync(e => e.ClubId == caminantes.Id, CancellationToken.None));
        Assert.Equal(1, await db.LiveSessions.CountAsync(l => l.ClubId == caminantes.Id, CancellationToken.None));

        var liveId = await db.LiveSessions.Where(l => l.ClubId == caminantes.Id).Select(l => l.Id).SingleAsync(CancellationToken.None);
        Assert.Equal(3, await db.LiveChatMessages.CountAsync(m => m.LiveSessionId == liveId, CancellationToken.None));
    }

    [Fact]
    public async Task Seed_EsIdempotente()
    {
        var db = await CreateSeededDbAsync();

        await ClubSeeder.SeedAsync(db, null, CancellationToken.None);
        await ClubSeeder.SeedAsync(db, null, CancellationToken.None);
        await using var _ = db;

        Assert.Equal(14, await db.Clubs.CountAsync(CancellationToken.None));
        Assert.Equal(9, await db.ClubCategories.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Seed_CreaPostsDeClub_ConEncuestaYEstadoProgramado()
    {
        var db = await CreateSeededDbAsync();
        await using var _ = db;

        var caminantes = await db.Clubs.SingleAsync(c => c.Slug == CaminantesSlug, CancellationToken.None);
        var posts = await db.Posts
            .Where(p => p.ClubId == caminantes.Id)
            .ToListAsync(CancellationToken.None);

        // Anuncio fijado público.
        Assert.Single(posts, p => p.Pinned && p.ClubVisibility == ClubPostVisibility.Publico);

        // Encuesta con opciones y votos.
        var encuesta = Assert.Single(posts, p => p.Type == PostType.Encuesta);
        var poll = await db.Polls.SingleAsync(p => p.PostId == encuesta.Id, CancellationToken.None);
        var optionIds = await db.PollOptions.Where(o => o.PollId == poll.Id).Select(o => o.Id).ToListAsync(CancellationToken.None);
        Assert.Equal(2, optionIds.Count);
        Assert.True(await db.PollVotes.CountAsync(v => optionIds.Contains(v.OptionId), CancellationToken.None) >= 5);

        // Post privado + programado.
        Assert.Single(posts, p => p.ClubVisibility == ClubPostVisibility.Privado);
        Assert.Single(posts, p => p.ClubStatus == ClubPostStatus.Programado && p.ScheduledFor != null);

        // Todos los clubes demo tienen al menos un post.
        Assert.True(await db.Posts.CountAsync(p => p.ClubId != null, CancellationToken.None) >= 9);
    }
}