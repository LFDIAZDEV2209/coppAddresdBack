using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoppAddresd.Community.GraphQL.Queries;

/// <summary>Consultas públicas y autenticadas de la comunidad.</summary>
public sealed class CommunityQuery
{
    /// <summary>Perfil del usuario autenticado (se crea en estado Pending la primera vez).</summary>
    [Authorize]
    public async Task<Profile?> Me(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUserId(http);
        if (userId is null) return null;

        var profile = await db.Profiles
            .Include(p => p.Posts)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null) return profile;

        // Auto-provisión: primer acceso crea un perfil pendiente de revisión.
        var created = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            DisplayName = "Miembro ANTARES",
            Status = ProfileStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        db.Profiles.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    [Authorize]
    public Task<Profile?> Profile(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => db.Profiles.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <summary>Feed de publicaciones no eliminadas (orden por fecha, pin primero).</summary>
    [Authorize]
    public async Task<IReadOnlyList<Post>> Feed(
        [Service] CommunityDbContext db,
        int take = 20,
        int skip = 0,
        CancellationToken ct = default)
        => await db.Posts
            .Include(p => p.Profile)
            .Where(p => p.DeletedAt == null)
            .OrderByDescending(p => p.Pinned)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    [Authorize]
    public Task<Post?> Post(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => db.Posts
            .Include(p => p.Profile)
            .Include(p => p.Comments)
            .ThenInclude(c => c.Replies)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);

    /// <summary>Perfiles pendientes de revisión (admin).</summary>
    [Authorize(Policy = "CommunityModerator")]
    public Task<List<Profile>> PendingProfiles(
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => db.Profiles
            .Where(p => p.Status == ProfileStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    internal static Guid? CurrentUserId(IHttpContextAccessor http)
        => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}

