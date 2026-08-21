using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Types;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using System.Security.Claims;

namespace CoppAddresd.Community.GraphQL.Queries;

/// <summary>Consultas públicas y autenticadas de la comunidad.</summary>
public sealed class CommunityQuery
{
    /// <summary>Perfil del usuario autenticado (se crea en estado Active la primera vez, sin revisión previa).</summary>
    [Authorize]
    public async Task<Profile?> Me(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUserId(http);
        if (userId is null) return null;

var profile = await db.Profiles
            .Include(p => p.Posts.Where(x => x.DeletedAt == null).OrderByDescending(x => x.CreatedAt))
                .ThenInclude(x => x.Likes)
            .Include(p => p.Posts.Where(x => x.DeletedAt == null).OrderByDescending(x => x.CreatedAt))
                .ThenInclude(x => x.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Posts.Where(x => x.DeletedAt == null).OrderByDescending(x => x.CreatedAt))
                .ThenInclude(x => x.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null) return profile;

        // Auto-provisión: primer acceso crea un perfil activo (sin revisión previa).
        var created = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            DisplayName = "Miembro ANTARES",
            Status = ProfileStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Profiles.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    [Authorize]
    public async Task<Profile?> Profile(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var profile = await db.Profiles
            .Include(p => p.Posts.Where(x => x.DeletedAt == null).OrderByDescending(x => x.CreatedAt))
                .ThenInclude(x => x.Likes)
            .Include(p => p.Posts.Where(x => x.DeletedAt == null).OrderByDescending(x => x.CreatedAt))
                .ThenInclude(x => x.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Posts.Where(x => x.DeletedAt == null).OrderByDescending(x => x.CreatedAt))
                .ThenInclude(x => x.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return profile;
    }

    /// <summary>Feed de publicaciones no eliminadas (orden por fecha, pin primero).</summary>
    [Authorize]
    public async Task<IReadOnlyList<Post>> Feed(
        [Service] CommunityDbContext db,
        int take = 20,
        int skip = 0,
        CancellationToken ct = default)
=> await db.Posts
            .Include(p => p.Profile)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
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
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .ThenInclude(c => c.Replies)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);

    /// <summary>Lista de perfiles con filtros opcionales por estado y búsqueda (moderador).</summary>
    [Authorize(Policy = "CommunityModerator")]
    public Task<List<Profile>> Profiles(
        ProfileStatus? status,
        string? search,
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var query = db.Profiles.AsQueryable();
        if (status is not null) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(EF.Functions.Unaccent(p.DisplayName), EF.Functions.Unaccent($"%{search}%")));
        return query
            .OrderBy(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Feed de publicaciones de los perfiles que sigo más las mías propias.</summary>
    [Authorize]
    public async Task<IReadOnlyList<Post>> FollowingFeed(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        int take = 20,
        int skip = 0,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId(http);
        if (userId is null) return [];
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return [];

        var followingIds = await db.Follows
            .Where(f => f.FollowerProfileId == profile.Id)
            .Select(f => f.FollowingProfileId)
            .ToListAsync(ct);

        return await db.Posts
            .Include(p => p.Profile)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .Where(p => p.DeletedAt == null && (p.ProfileId == profile.Id || followingIds.Contains(p.ProfileId)))
            .OrderByDescending(p => p.Pinned)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Catálogo de perfiles activos con indicadores de relación respecto al usuario actual.</summary>
    [Authorize]
    public async Task<List<Person>> People(
        string? search,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 30,
        int skip = 0)
    {
        var userId = CurrentUserId(http);
        var myProfile = userId is null
            ? null
            : await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var query = db.Profiles.Where(p => p.Status == ProfileStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(EF.Functions.Unaccent(p.DisplayName), EF.Functions.Unaccent($"%{search}%")));

        var profiles = await query
            .OrderBy(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        if (myProfile is null)
        {
            return profiles.Select(p => new Person
            {
                Profile = p,
                IsFollowing = false,
                IsFollower = false,
                IsFriend = false,
            }).ToList();
        }

        var myFollowingIds = await db.Follows
            .Where(f => f.FollowerProfileId == myProfile.Id)
            .Select(f => f.FollowingProfileId)
            .ToListAsync(ct);
        var myFollowerIds = await db.Follows
            .Where(f => f.FollowingProfileId == myProfile.Id)
            .Select(f => f.FollowerProfileId)
            .ToListAsync(ct);

        return profiles.Select(p => new Person
        {
            Profile = p,
            IsFollowing = myFollowingIds.Contains(p.Id),
            IsFollower = myFollowerIds.Contains(p.Id),
            IsFriend = myFollowingIds.Contains(p.Id) && myFollowerIds.Contains(p.Id),
        }).ToList();
    }

    /// <summary>Perfiles que sigo (orden por fecha de seguimiento, más reciente primero).</summary>
    [Authorize]
    public async Task<List<Profile>> Following(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var ids = await db.Follows
            .Where(f => f.FollowerProfileId == profile.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.FollowingProfileId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        var loaded = await db.Profiles.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var byId = loaded.ToDictionary(p => p.Id);
        return ids.Select(id => byId[id]).ToList();
    }

    /// <summary>Perfiles que me siguen (orden por fecha de seguimiento, más reciente primero).</summary>
    [Authorize]
    public async Task<List<Profile>> Followers(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var ids = await db.Follows
            .Where(f => f.FollowingProfileId == profile.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.FollowerProfileId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        var loaded = await db.Profiles.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var byId = loaded.ToDictionary(p => p.Id);
        return ids.Select(id => byId[id]).ToList();
    }

    /// <summary>Amigos: intersección de siguiendo y seguidores (orden por fecha de seguimiento).</summary>
    [Authorize]
    public async Task<List<Profile>> Friends(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var following = await db.Follows
            .Where(f => f.FollowerProfileId == profile.Id)
            .Select(f => new { f.FollowingProfileId, f.CreatedAt })
            .ToListAsync(ct);
        var followerIds = await db.Follows
            .Where(f => f.FollowingProfileId == profile.Id)
            .Select(f => f.FollowerProfileId)
            .ToListAsync(ct);

        var friendIds = following
            .Where(f => followerIds.Contains(f.FollowingProfileId))
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(f => f.FollowingProfileId)
            .ToList();

        var loaded = await db.Profiles.Where(p => friendIds.Contains(p.Id)).ToListAsync(ct);
        var byId = loaded.ToDictionary(p => p.Id);
        return friendIds.Select(id => byId[id]).ToList();
    }

    /// <summary>Resumen de conversaciones del usuario (último mensaje por interlocutor).</summary>
    [Authorize]
    public async Task<List<Conversation>> Conversations(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var messages = await db.Messages
            .Where(m => m.SenderProfileId == profile.Id || m.RecipientProfileId == profile.Id)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        var groups = messages
            .GroupBy(m => m.SenderProfileId == profile.Id ? m.RecipientProfileId : m.SenderProfileId)
            .Select(g => new
            {
                PeerId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).First(),
            })
            .OrderByDescending(x => x.LastMessage.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        var peerIds = groups.Select(g => g.PeerId).ToList();
        var peers = await db.Profiles.Where(p => peerIds.Contains(p.Id)).ToListAsync(ct);
        var byId = peers.ToDictionary(p => p.Id);
        return groups.Select(g => new Conversation
        {
            Peer = byId[g.PeerId],
            LastMessage = g.LastMessage,
        }).ToList();
    }

    /// <summary>Historial de mensajes con un interlocutor específico (más recientes primero).</summary>
    [Authorize]
    public async Task<List<Message>> Conversation(
        Guid peerId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        return await db.Messages
            .Where(m => (m.SenderProfileId == profile.Id && m.RecipientProfileId == peerId)
                     || (m.SenderProfileId == peerId && m.RecipientProfileId == profile.Id))
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    private static async Task<Profile> RequireMyProfileAsync(
        CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
        => await db.Profiles.FirstOrDefaultAsync(p => p.UserId == CurrentUserId(http), ct)
           ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");

    internal static Guid? CurrentUserId(IHttpContextAccessor http)
        => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}

