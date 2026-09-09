using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using CoppAddresd.Community.Security;
using CoppAddresd.Community.Storage;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Resolvers;

/// <summary>
/// Resolvers derivados del módulo de clubes: membresía del solicitante y
/// URLs firmadas de portada/logo.
/// </summary>
[ExtendObjectType(typeof(Club))]
public sealed class ClubResolvers
{
    /// <summary>Membresía del usuario autenticado en el club (null si no es miembro).</summary>
    public async Task<ClubMember?> MyMembership(
        [Parent] Club club,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var myId = CommunityQuery.CurrentUserId(http);
        if (myId is null) return null;

        var profileId = await db.Profiles.Where(p => p.UserId == myId).Select(p => p.Id).FirstOrDefaultAsync(ct);
        if (profileId == Guid.Empty) return null;

        return await db.ClubMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ClubId == club.Id && m.ProfileId == profileId, ct);
    }
}

/// <summary>Resolver de asistencia del solicitante a un evento del club.</summary>
[ExtendObjectType(typeof(ClubEvent))]
public sealed class ClubEventResolvers
{
    /// <summary>Asistencia del usuario autenticado al evento (null si no tiene).</summary>
    public async Task<EventAttendance?> MyAttendance(
        [Parent] ClubEvent @event,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var myId = CommunityQuery.CurrentUserId(http);
        if (myId is null) return null;

        var profileId = await db.Profiles.Where(p => p.UserId == myId).Select(p => p.Id).FirstOrDefaultAsync(ct);
        if (profileId == Guid.Empty) return null;

        return await db.EventAttendances
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EventId == @event.Id && a.ProfileId == profileId, ct);
    }
}

/// <summary>Resolvers de media del club: portada y logo con URL firmada.</summary>
[ExtendObjectType(typeof(Club))]
public sealed class ClubMediaResolvers
{
    private static string SignedUrl(string? key, StorageSignatureService signer, IConfiguration config)
    {
        if (key is null) return null!;
        var publicBase = config["Storage:PublicBaseUrl"] ?? string.Empty;
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        return $"{publicBase}/storage/{key}?sig={signer.Sign(key, expiresAt)}&exp={expiresAt.ToUnixTimeSeconds()}";
    }

    /// <summary>URL firmada de la portada (null si el club no tiene portada).</summary>
    public string? CoverUrl(
        [Parent] Club club,
        [Service] StorageSignatureService signer,
        [Service] IConfiguration config)
        => SignedUrl(club.CoverKey, signer, config);

    /// <summary>URL firmada del logo (null si el club no tiene logo).</summary>
    public string? LogoUrl(
        [Parent] Club club,
        [Service] StorageSignatureService signer,
        [Service] IConfiguration config)
        => SignedUrl(club.LogoKey, signer, config);
}