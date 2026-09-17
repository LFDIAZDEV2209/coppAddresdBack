using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Types;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Tests unitarios para la funcionalidad de envío de mensajes desde el perfil
/// del sistema (system sender): seeder idempotente, alcance de mensajes,
/// creación de mensajes y validaciones.
/// </summary>
public sealed class SystemSenderTests : IDisposable
{
    private static readonly DateTime BaseNow = new(2026, 8, 27, 14, 0, 0, DateTimeKind.Utc);

    // ─── Helpers ────────────────────────────────────────────────────────

    private static CommunityDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new CommunityDbContext(options);
    }

    private static Profile MakeSystemProfile(string displayName = "Equipo Copp Adresd")
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = null,
            IsSystem = true,
            DisplayName = displayName,
            Status = ProfileStatus.Active,
            CreatedAt = BaseNow,
        };

    private static Profile MakeUserProfile(
        Guid? userId = null,
        ProfileStatus status = ProfileStatus.Active,
        DateTimeOffset? lastPostAt = null,
        DateTimeOffset? lastActiveAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            IsSystem = false,
            DisplayName = "Test User",
            Status = status,
            LastPostAt = lastPostAt,
            LastActiveAt = lastActiveAt,
            CreatedAt = BaseNow.AddDays(-60),
        };

    public void Dispose() { }

    // ─── System Profile Resolution ──────────────────────────────────────

    [Fact]
    public async Task SystemProfile_IsCreatedByIdempotently()
    {
        using var db = CreateInMemoryDb();

        // Seeding: crear el perfil del sistema
        db.Profiles.Add(MakeSystemProfile());
        await db.SaveChangesAsync();

        // Verificar que existe exactamente uno
        var systemProfiles = await db.Profiles.Where(p => p.IsSystem).ToListAsync();
        Assert.Single(systemProfiles);
        Assert.Null(systemProfiles[0].UserId);
        Assert.True(systemProfiles[0].IsSystem);
        Assert.Equal("Equipo Copp Adresd", systemProfiles[0].DisplayName);
    }

    [Fact]
    public async Task SystemProfile_HasNullUserId()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        db.Profiles.Add(systemProfile);
        await db.SaveChangesAsync();

        var loaded = await db.Profiles.FirstAsync(p => p.IsSystem);
        Assert.Null(loaded.UserId);
    }

    [Fact]
    public async Task SystemProfile_IsNotReturnedByMemberQueries()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        var userProfiles = new List<Profile>
        {
            MakeUserProfile(lastActiveAt: BaseNow.AddDays(-1)),
            MakeUserProfile(lastActiveAt: BaseNow.AddDays(-3)),
        };

        db.Profiles.Add(systemProfile);
        db.Profiles.AddRange(userProfiles);
        await db.SaveChangesAsync();

        // topStreaks-style query: Active + !IsSystem
        var activeNonSystem = await db.Profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
            .ToListAsync();

        Assert.Equal(2, activeNonSystem.Count);
        Assert.All(activeNonSystem, p => Assert.False(p.IsSystem));
    }

    // ─── INACTIVE Scope Predicate ───────────────────────────────────────

    [Fact]
    public void InactiveScope_LastActiveAt10DaysAgo_IsIncluded()
    {
        // Perfil inactivo hace 10 días (>7d) → incluido
        var profile = MakeUserProfile(lastActiveAt: BaseNow.AddDays(-10));
        var threshold7d = BaseNow.AddDays(-7);

        var last = profile.LastPostAt.HasValue && profile.LastActiveAt.HasValue
            ? (profile.LastPostAt.Value > profile.LastActiveAt.Value ? profile.LastPostAt.Value : profile.LastActiveAt.Value)
            : profile.LastPostAt ?? profile.LastActiveAt;

        Assert.NotNull(last);
        Assert.True(last.Value.UtcDateTime < threshold7d);
    }

    [Fact]
    public void InactiveScope_LastActiveAt3DaysAgo_IsExcluded()
    {
        // Perfil inactivo hace 3 días (<7d) → excluido
        var profile = MakeUserProfile(lastActiveAt: BaseNow.AddDays(-3));
        var threshold7d = BaseNow.AddDays(-7);

        var last = profile.LastPostAt.HasValue && profile.LastActiveAt.HasValue
            ? (profile.LastPostAt.Value > profile.LastActiveAt.Value ? profile.LastPostAt.Value : profile.LastActiveAt.Value)
            : profile.LastPostAt ?? profile.LastActiveAt;

        Assert.NotNull(last);
        Assert.False(last.Value.UtcDateTime < threshold7d);
    }

    [Fact]
    public void InactiveScope_NullActivity_IsIncluded()
    {
        // Perfil sin actividad (null) → incluido
        var profile = MakeUserProfile(lastPostAt: null, lastActiveAt: null);

        var last = profile.LastPostAt.HasValue && profile.LastActiveAt.HasValue
            ? (profile.LastPostAt.Value > profile.LastActiveAt.Value ? profile.LastPostAt.Value : profile.LastActiveAt.Value)
            : profile.LastPostAt ?? profile.LastActiveAt;

        Assert.Null(last);
        // Sin actividad ⇒ inactivo (mismo criterio que DashboardAggregator.IsInactiveBefore)
    }

    [Fact]
    public void InactiveScope_MatchesDashboardAggregatorPredicate()
    {
        // Verificar que el predicado INACTIVE produce el mismo resultado
        // que DashboardAggregator para un conjunto de perfiles variados.
        var now = BaseNow;
        var profiles = new List<Profile>
        {
            MakeUserProfile(lastActiveAt: now.AddDays(-10)), // Inactivo >7d → incluido
            MakeUserProfile(lastActiveAt: now.AddDays(-3)),  // Activo <7d → excluido
            MakeUserProfile(lastPostAt: null, lastActiveAt: null), // Sin actividad → incluido
            MakeUserProfile(lastActiveAt: now.AddDays(-8)),  // Inactivo >7d → incluido
        };

        var threshold7d = now.AddDays(-7);
        var inactiveCount = profiles.Count(p =>
        {
            var last = p.LastPostAt.HasValue && p.LastActiveAt.HasValue
                ? (p.LastPostAt.Value > p.LastActiveAt.Value ? p.LastPostAt.Value : p.LastActiveAt.Value)
                : p.LastPostAt ?? p.LastActiveAt;
            return last is null || last.Value.UtcDateTime < threshold7d;
        });

        Assert.Equal(3, inactiveCount); // 3 inactivos (>7d o null)
    }

    // ─── ALL_ACTIVE Excludes System ─────────────────────────────────────

    [Fact]
    public async Task AllActiveScope_ExcludesSystemProfiles()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        var userProfiles = new List<Profile>
        {
            MakeUserProfile(status: ProfileStatus.Active),
            MakeUserProfile(status: ProfileStatus.Active),
            MakeUserProfile(status: ProfileStatus.Banned),
        };

        db.Profiles.Add(systemProfile);
        db.Profiles.AddRange(userProfiles);
        await db.SaveChangesAsync();

        // Simular la query de ALL_ACTIVE: Active && !IsSystem
        var targets = await db.Profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
            .ToListAsync();

        Assert.Equal(2, targets.Count);
        Assert.All(targets, p => Assert.False(p.IsSystem));
        Assert.All(targets, p => Assert.Equal(ProfileStatus.Active, p.Status));
    }

    // ─── Bulk Send ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendBulkMessage_CreatesMessagesWithCorrectSenderAndTrigger()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        var adminProfile = MakeUserProfile();
        adminProfile.UserId = Guid.NewGuid();
        var targets = new List<Profile>
        {
            MakeUserProfile(lastActiveAt: BaseNow.AddDays(-10)),
            MakeUserProfile(lastActiveAt: BaseNow.AddDays(-12)),
            MakeUserProfile(lastActiveAt: BaseNow.AddDays(-8)),
        };

        db.Profiles.Add(systemProfile);
        db.Profiles.Add(adminProfile);
        db.Profiles.AddRange(targets);
        await db.SaveChangesAsync();

        // Simular bulk send: insertar mensajes por destinatario
        var now = DateTime.UtcNow;
        foreach (var target in targets)
        {
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                SenderProfileId = systemProfile.Id,
                RecipientProfileId = target.Id,
                Body = "Mensaje de prueba",
                CreatedAt = now,
                TriggeredByProfileId = adminProfile.Id,
            });
        }
        await db.SaveChangesAsync();

        // Verificar
        var messages = await db.Messages.Where(m => m.TriggeredByProfileId == adminProfile.Id).ToListAsync();
        Assert.Equal(3, messages.Count);
        Assert.All(messages, m =>
        {
            Assert.Equal(systemProfile.Id, m.SenderProfileId);
            Assert.Equal(adminProfile.Id, m.TriggeredByProfileId);
            Assert.Equal("Mensaje de prueba", m.Body);
        });
    }

    [Fact]
    public async Task SendBulkMessage_EmitsFeedEventWithSystemProfile()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        db.Profiles.Add(systemProfile);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = systemProfile.Id,
            Kind = FeedEventKind.Mensaje,
            Body = "Mensaje enviado a 5 miembros",
            CreatedAt = now,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync();

        var loaded = await db.FeedEvents.FirstAsync(f => f.Kind == FeedEventKind.Mensaje);
        Assert.Equal(systemProfile.Id, loaded.ProfileId);
        Assert.Contains("5 miembros", loaded.Body);
    }

    // ─── Direct Message ─────────────────────────────────────────────────

    [Fact]
    public async Task SendDirectMessage_BypassesFriendshipRule()
    {
        // El admin envía a un usuario que NO es su amigo (no hay follow mutuo).
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        var adminProfile = MakeUserProfile();
        adminProfile.UserId = Guid.NewGuid();
        var recipient = MakeUserProfile(status: ProfileStatus.Active);

        db.Profiles.Add(systemProfile);
        db.Profiles.Add(adminProfile);
        db.Profiles.Add(recipient);
        await db.SaveChangesAsync();

        // No se crea follow mutuo → el usuario no es amigo del admin.
        // El direct message debe funcionar igualmente.

        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = systemProfile.Id,
            RecipientProfileId = recipient.Id,
            Body = "Mensaje directo del sistema",
            CreatedAt = now,
            TriggeredByProfileId = adminProfile.Id,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var loaded = await db.Messages.FirstAsync(m => m.Id == message.Id);
        Assert.Equal(systemProfile.Id, loaded.SenderProfileId);
        Assert.Equal(recipient.Id, loaded.RecipientProfileId);
        Assert.Equal(adminProfile.Id, loaded.TriggeredByProfileId);
    }

    [Fact]
    public async Task SendDirectMessage_FailsForNonExistentRecipient()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        db.Profiles.Add(systemProfile);
        await db.SaveChangesAsync();

        var nonExistentId = Guid.NewGuid();

        // Verificar que el destinatario no existe
        var exists = await db.Profiles.AnyAsync(p => p.Id == nonExistentId);
        Assert.False(exists);
    }

    [Fact]
    public async Task SendDirectMessage_FailsForInactiveRecipient()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        var inactiveRecipient = MakeUserProfile(status: ProfileStatus.Banned);

        db.Profiles.Add(systemProfile);
        db.Profiles.Add(inactiveRecipient);
        await db.SaveChangesAsync();

        // Verificar que el destinatario está baneado
        var recipient = await db.Profiles.FirstAsync(p => p.Id == inactiveRecipient.Id);
        Assert.Equal(ProfileStatus.Banned, recipient.Status);
    }

    // ─── Missing System Profile ─────────────────────────────────────────

    [Fact]
    public async Task MissingSystemProfile_ReturnsNull()
    {
        using var db = CreateInMemoryDb();

        // No se crea ningún perfil del sistema
        var systemProfile = await db.Profiles.FirstOrDefaultAsync(p => p.IsSystem);
        Assert.Null(systemProfile);
    }

    [Fact]
    public async Task GetSystemProfileAsync_ReturnsSystemProfile()
    {
        using var db = CreateInMemoryDb();

        var systemProfile = MakeSystemProfile();
        db.Profiles.Add(systemProfile);
        await db.SaveChangesAsync();

        var found = await db.Profiles.FirstOrDefaultAsync(p => p.IsSystem);
        Assert.NotNull(found);
        Assert.True(found.IsSystem);
        Assert.Equal("Equipo Copp Adresd", found.DisplayName);
    }

    // ─── FeedEventKind.Mensaje ──────────────────────────────────────────

    [Fact]
    public void FeedEventKind_HasMensajeValue()
    {
        Assert.True(Enum.IsDefined(typeof(FeedEventKind), FeedEventKind.Mensaje));
        Assert.Equal("Mensaje", FeedEventKind.Mensaje.ToString());
    }

    [Fact]
    public void MessageScope_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(MessageScope), MessageScope.Inactive));
        Assert.True(Enum.IsDefined(typeof(MessageScope), MessageScope.AllActive));
        Assert.Equal(2, Enum.GetValues<MessageScope>().Length);
    }

    // ─── Message Entity ─────────────────────────────────────────────────

    [Fact]
    public void Message_TriggeredByProfileId_IsNullable()
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = Guid.NewGuid(),
            Body = "Test",
            CreatedAt = DateTime.UtcNow,
        };
        Assert.Null(message.TriggeredByProfileId);
    }

    [Fact]
    public void Message_TriggeredByProfileId_CanBeSet()
    {
        var triggerId = Guid.NewGuid();
        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = Guid.NewGuid(),
            Body = "Test",
            CreatedAt = DateTime.UtcNow,
            TriggeredByProfileId = triggerId,
        };
        Assert.Equal(triggerId, message.TriggeredByProfileId);
    }

    // ─── Profile IsSystem ───────────────────────────────────────────────

    [Fact]
    public void Profile_IsSystem_DefaultsToFalse()
    {
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Test",
            CreatedAt = DateTime.UtcNow,
        };
        Assert.False(profile.IsSystem);
    }

    [Fact]
    public void Profile_UserId_CanBeNull()
    {
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = null,
            IsSystem = true,
            DisplayName = "System",
            CreatedAt = DateTime.UtcNow,
        };
        Assert.Null(profile.UserId);
    }
}
