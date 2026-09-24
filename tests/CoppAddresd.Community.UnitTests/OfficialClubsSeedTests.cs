using CoppAddresd.Community;
using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Seeds de la Fase 10 en <c>ClubSeeder</c>: los 5 clubes oficiales de sistema
/// y las membresías + posts demo de los pacientes de prueba.
/// </summary>
public sealed class OfficialClubsSeedTests
{
    private static readonly (string Slug, string Name)[] ExpectedClubs =
    [
        ("nutricion-saludable", "Nutrición Saludable"),
        ("movimiento-y-ejercicio", "Movimiento y Ejercicio"),
        ("mente-y-bienestar", "Mente y Bienestar"),
        ("habitos-y-sueno", "Hábitos y Sueño"),
        ("comunidad-general", "Comunidad General"),
    ];

    private static CommunityDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new CommunityDbContext(options);
    }

    private static Profile MakeSystemProfile() =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = null,
            IsSystem = true,
            DisplayName = "Equipo Copp Adresd",
            Status = ProfileStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };

    private static List<ClubSeeder.TestPatientSeed> MakePatients() =>
        [
            new("55551234", Guid.NewGuid(), "Luis Prueba Movil"),
            new("77777777", Guid.NewGuid(), "Play Wright"),
            new("88888888", Guid.NewGuid(), "Test E2E"),
            new("1012345678", Guid.NewGuid(), "Andrea Salazar"),
        ];

    [Fact]
    public async Task SeedOfficialClubsAsync_CreaLos5ClubesDeSistema()
    {
        using var db = CreateInMemoryDb();
        var system = MakeSystemProfile();
        db.Profiles.Add(system);
        await db.SaveChangesAsync();

        var clubs = await ClubSeeder.SeedOfficialClubsAsync(db, system);

        Assert.Equal(5, clubs.Count);
        foreach (var (slug, name) in ExpectedClubs)
        {
            var club = Assert.Single(await db.Clubs.Where(c => c.Slug == slug).ToListAsync());
            Assert.Equal(name, club.Name);
            Assert.True(club.IsSystem);
            Assert.Equal(system.Id, club.CreatedByProfileId);
            Assert.Equal(ClubStatus.Activo, club.Status);
        }
    }

    [Fact]
    public async Task SeedOfficialClubsAsync_EsIdempotente()
    {
        using var db = CreateInMemoryDb();
        var system = MakeSystemProfile();
        db.Profiles.Add(system);
        await db.SaveChangesAsync();

        await ClubSeeder.SeedOfficialClubsAsync(db, system);
        await ClubSeeder.SeedOfficialClubsAsync(db, system);

        Assert.Equal(5, await db.Clubs.CountAsync(c => c.IsSystem));
        foreach (var (slug, _) in ExpectedClubs)
            Assert.Equal(1, await db.Clubs.CountAsync(c => c.Slug == slug));
    }

    [Fact]
    public async Task SeedOfficialClubsAsync_RespetaClubesExistentes()
    {
        using var db = CreateInMemoryDb();
        var system = MakeSystemProfile();
        db.Profiles.Add(system);
        db.Clubs.Add(
            new Club
            {
                Id = Guid.NewGuid(),
                Slug = "comunidad-general",
                Name = "Comunidad General (personalizado)",
                Description = "Club preexistente",
                Category = "Salud",
                CreatedByProfileId = system.Id,
                IsSystem = false,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var clubs = await ClubSeeder.SeedOfficialClubsAsync(db, system);

        Assert.Equal(5, clubs.Count);
        var general = Assert.Single(
            await db.Clubs.Where(c => c.Slug == "comunidad-general").ToListAsync()
        );
        Assert.False(general.IsSystem);
        Assert.Equal("Comunidad General (personalizado)", general.Name);
        Assert.Equal(5, await db.Clubs.CountAsync());
    }

    [Fact]
    public async Task SeedTestPatientMemberships_CreaPerfilesMembresiasYPost()
    {
        using var db = CreateInMemoryDb();
        var system = MakeSystemProfile();
        db.Profiles.Add(system);
        await db.SaveChangesAsync();
        var clubs = await ClubSeeder.SeedOfficialClubsAsync(db, system);
        var patients = MakePatients();

        await ClubSeeder.SeedTestPatientMembershipsAsync(db, clubs, patients);

        // 4 perfiles vinculados por UserId (más el de sistema).
        Assert.Equal(5, await db.Profiles.CountAsync());
        foreach (var patient in patients)
            Assert.NotNull(await db.Profiles.SingleOrDefaultAsync(p => p.UserId == patient.UserId));

        // Membresía activa en cada club oficial por paciente: 5 x 4.
        Assert.Equal(
            20,
            await db.ClubMembers.CountAsync(m =>
                m.Status == ClubMemberStatus.Activo && m.Role == ClubMemberRole.Miembro
            )
        );

        // Un post demo por paciente en Comunidad General.
        var general = clubs.Single(c => c.Slug == "comunidad-general");
        Assert.Equal(4, await db.Posts.CountAsync(p => p.ClubId == general.Id));
    }

    [Fact]
    public async Task SeedTestPatientMemberships_EsIdempotente()
    {
        using var db = CreateInMemoryDb();
        var system = MakeSystemProfile();
        db.Profiles.Add(system);
        await db.SaveChangesAsync();
        var clubs = await ClubSeeder.SeedOfficialClubsAsync(db, system);
        var patients = MakePatients();

        await ClubSeeder.SeedTestPatientMembershipsAsync(db, clubs, patients);
        await ClubSeeder.SeedTestPatientMembershipsAsync(db, clubs, patients);

        Assert.Equal(5, await db.Profiles.CountAsync());
        Assert.Equal(20, await db.ClubMembers.CountAsync());
        var general = clubs.Single(c => c.Slug == "comunidad-general");
        Assert.Equal(4, await db.Posts.CountAsync(p => p.ClubId == general.Id));
    }

    [Fact]
    public async Task SeedTestPatientMemberships_SinClubes_NoHaceNada()
    {
        using var db = CreateInMemoryDb();

        await ClubSeeder.SeedTestPatientMembershipsAsync(db, []);

        Assert.Equal(0, await db.Profiles.CountAsync());
        Assert.Equal(0, await db.ClubMembers.CountAsync());
        Assert.Equal(0, await db.Posts.CountAsync());
    }

    [Fact]
    public async Task SeedTestPatientMemberships_RespetaMembresiaExistente()
    {
        using var db = CreateInMemoryDb();
        var system = MakeSystemProfile();
        db.Profiles.Add(system);
        await db.SaveChangesAsync();
        var clubs = await ClubSeeder.SeedOfficialClubsAsync(db, system);
        var patients = MakePatients();

        // Un paciente con solicitud pendiente en un club: no se pisa.
        var pendingProfile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = patients[0].UserId,
            DisplayName = patients[0].DisplayName,
            Status = ProfileStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Profiles.Add(pendingProfile);
        db.ClubMembers.Add(
            new ClubMember
            {
                ClubId = clubs[0].Id,
                ProfileId = pendingProfile.Id,
                Role = ClubMemberRole.Miembro,
                Status = ClubMemberStatus.Pendiente,
                JoinedAt = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        await ClubSeeder.SeedTestPatientMembershipsAsync(db, clubs, patients);

        var kept = await db.ClubMembers.SingleAsync(m =>
            m.ClubId == clubs[0].Id && m.ProfileId == pendingProfile.Id
        );
        Assert.Equal(ClubMemberStatus.Pendiente, kept.Status);
        Assert.Equal(20, await db.ClubMembers.CountAsync());
    }
}
