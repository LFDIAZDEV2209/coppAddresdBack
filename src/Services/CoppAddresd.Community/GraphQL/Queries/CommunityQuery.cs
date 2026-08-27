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
        // El DisplayName se toma del claim Name del JWT (Auth emite "FirstName LastName");
        // si no viene, se usa el nombre del identity o un valor por defecto.
        var created = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            DisplayName = DisplayNameFromClaims(http) ?? "Miembro ANTARES",
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

    /// <summary>
    /// Feed de publicaciones no eliminadas (orden por fecha, pin primero) con filtros
    /// opcionales de moderación: por autor (coincidencia parcial, sin acentos), por
    /// texto del cuerpo (coincidencia parcial, sin acentos) y por rango de fechas.
    /// </summary>
    /// <param name="db">Contexto de la comunidad.</param>
    /// <param name="author">Filtra por autor (coincidencia parcial, sin acentos).</param>
    /// <param name="search">Filtra por texto en el cuerpo (coincidencia parcial, sin acentos).</param>
    /// <param name="from">Incluye publicaciones creadas a partir de esta fecha (inclusive).</param>
    /// <param name="to">Incluye publicaciones creadas hasta el final de este día (inclusive).</param>
    /// <param name="take">Cantidad máxima de resultados a devolver.</param>
    /// <param name="skip">Cantidad de resultados a omitir (paginación).</param>
    /// <param name="ct">Token de cancelación.</param>
    [Authorize]
    public async Task<IReadOnlyList<Post>> Feed(
        [Service] CommunityDbContext db,
        string? author = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        int take = 20,
        int skip = 0,
        CancellationToken ct = default)
    {
        var query = db.Posts
            .Include(p => p.Profile)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .Where(p => p.DeletedAt == null);

        // Filtro por autor: coincidencia parcial e insensible a acentos (ILike + Unaccent),
        // siguiendo el mismo patrón de búsqueda de perfiles en Profiles(...).
        if (!string.IsNullOrWhiteSpace(author))
            query = query.Where(p => EF.Functions.ILike(
                EF.Functions.Unaccent(p.Profile.DisplayName),
                EF.Functions.Unaccent($"%{author}%")));

        // Filtro por texto del cuerpo: coincidencia parcial e insensible a acentos.
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(
                EF.Functions.Unaccent(p.Body),
                EF.Functions.Unaccent($"%{search}%")));

        // Filtro por fecha de creación (rango inclusivo por día en `to`).
        if (from is not null)
            query = query.Where(p => p.CreatedAt >= from);
        if (to is not null)
            query = query.Where(p => p.CreatedAt < to.Value.AddDays(1));

        return await query
            .OrderByDescending(p => p.Pinned)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

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

    /// <summary>Feed de publicaciones de los perfiles que sigo (sin incluir las propias).</summary>
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
            .Where(p => p.DeletedAt == null && followingIds.Contains(p.ProfileId))
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

    /// <summary>Seguidores de cualquier perfil (orden por fecha de seguimiento, más reciente primero).</summary>
    [Authorize]
    public async Task<List<Profile>> ProfileFollowers(
        Guid profileId,
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var ids = await db.Follows
            .Where(f => f.FollowingProfileId == profileId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.FollowerProfileId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        var loaded = await db.Profiles.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var byId = loaded.ToDictionary(p => p.Id);
        return ids.Select(id => byId[id]).ToList();
    }

    /// <summary>Perfiles que sigue cualquier perfil (orden por fecha de seguimiento, más reciente primero).</summary>
    [Authorize]
    public async Task<List<Profile>> ProfileFollowing(
        Guid profileId,
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var ids = await db.Follows
            .Where(f => f.FollowerProfileId == profileId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.FollowingProfileId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        var loaded = await db.Profiles.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var byId = loaded.ToDictionary(p => p.Id);
        return ids.Select(id => byId[id]).ToList();
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

        // "Último mensaje por par" en el propio servidor (DISTINCT ON en PostgreSQL):
        // evita cargar todo el historial del usuario en memoria para agruparlo.
        var sql = """
            SELECT y.id, y.sender_profile_id, y.recipient_profile_id, y.conversation_id, y.body, y.created_at
            FROM (
                SELECT DISTINCT ON (x.peer_id) x.id, x.sender_profile_id, x.recipient_profile_id,
                       x.conversation_id, x.body, x.created_at
                FROM (
                    SELECT m.id, m.sender_profile_id, m.recipient_profile_id, m.conversation_id, m.body, m.created_at,
                           CASE WHEN m.sender_profile_id = {0}
                                THEN m.recipient_profile_id ELSE m.sender_profile_id END AS peer_id
                    FROM community.messages m
                    WHERE (m.sender_profile_id = {0} OR m.recipient_profile_id = {0})
                      AND m.conversation_id IS NULL
                ) x
                ORDER BY x.peer_id, x.created_at DESC
            ) y
            ORDER BY y.created_at DESC
            LIMIT {1} OFFSET {2}
            """;
        var latest = await db.Messages.FromSqlRaw(sql, profile.Id, take, skip).ToListAsync(ct);

        // En Conversations solo aparecen mensajes 1:1 (conversation_id IS NULL), por
        // lo que recipient_profile_id siempre es no nulo; el ! es seguro aquí.
        var peerIds = latest.Select(m => m.SenderProfileId == profile.Id ? m.RecipientProfileId!.Value : m.SenderProfileId).ToList();
        var peers = await db.Profiles.Where(p => peerIds.Contains(p.Id)).ToListAsync(ct);
        var byId = peers.ToDictionary(p => p.Id);
        return latest.Select(m => new Conversation
        {
            Peer = byId[m.SenderProfileId == profile.Id ? m.RecipientProfileId!.Value : m.SenderProfileId],
            LastMessage = m,
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
            .Where(m => ((m.SenderProfileId == profile.Id && m.RecipientProfileId == peerId)
                      || (m.SenderProfileId == peerId && m.RecipientProfileId == profile.Id))
                      && m.ConversationId == null)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Mis grupos de chat: con último mensaje y cantidad de miembros (sin N+1).</summary>
    [Authorize]
    public async Task<IReadOnlyList<ChatGroup>> Groups(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var myGroupIds = await db.ChatGroupMembers
            .Where(m => m.ProfileId == profile.Id)
            .Select(m => m.GroupId)
            .ToListAsync(ct);
        if (myGroupIds.Count == 0) return [];

        var groups = await db.ChatGroups
            .Where(g => myGroupIds.Contains(g.Id))
            .ToListAsync(ct);
        var groupIds = groups.Select(g => g.Id).ToList();

        // Cantidad de miembros por grupo en una sola consulta (evita N+1).
        var memberCounts = await db.ChatGroupMembers
            .Where(m => groupIds.Contains(m.GroupId))
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countById = memberCounts.ToDictionary(x => x.GroupId, x => x.Count);

        // Último mensaje por grupo con DISTINCT ON (una sola consulta).
        var lastSql = """
            SELECT DISTINCT ON (m.conversation_id) m.id, m.sender_profile_id, m.recipient_profile_id,
                   m.conversation_id, m.body, m.created_at
            FROM community.messages m
            WHERE m.conversation_id = ANY({0})
            ORDER BY m.conversation_id, m.created_at DESC
            """;
        var lastMessages = await db.Messages.FromSqlRaw(lastSql, groupIds.ToArray()).ToListAsync(ct);
        var lastByGroup = lastMessages.ToDictionary(m => m.ConversationId!.Value, m => m);

        foreach (var g in groups)
        {
            g.MemberCount = countById.GetValueOrDefault(g.Id, 0);
            lastByGroup.TryGetValue(g.Id, out var last);
            g.LastMessage = last;
        }

        // Grupos con mensaje más reciente primero; los que no tienen mensaje van al final.
        return groups
            .OrderByDescending(g => g.LastMessage?.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(g => g.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    /// <summary>Historial de un grupo de chat (más recientes primero). Requiere ser miembro.</summary>
    [Authorize]
    public async Task<IReadOnlyList<Message>> Group(
        Guid groupId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var isMember = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct);
        if (!isMember) throw new GraphQLException("No eres miembro de este grupo.");

        return await db.Messages
            .Where(m => m.ConversationId == groupId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Miembros de un grupo de chat. Requiere ser miembro.</summary>
    [Authorize]
    public async Task<IReadOnlyList<Profile>> GroupMembers(
        Guid groupId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var isMember = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct);
        if (!isMember) throw new GraphQLException("No eres miembro de este grupo.");

        var profileIds = await db.ChatGroupMembers
            .Where(m => m.GroupId == groupId)
            .Select(m => m.ProfileId)
            .ToListAsync(ct);
        return await db.Profiles.Where(p => profileIds.Contains(p.Id)).ToListAsync(ct);
    }

    /// <summary>
    /// Valida autenticación, perfil y membresía al grupo. Lanza 'No estás autenticado.',
    /// 'Crea tu perfil de comunidad primero.' o 'No eres miembro de este grupo.' según corresponda.
    /// </summary>
    internal static async Task<ChatGroup> RequireGroupMembershipAsync(
        CommunityDbContext db, IHttpContextAccessor http, Guid groupId, CancellationToken ct)
    {
        var userId = CurrentUserId(http);
        if (userId is null) throw new GraphQLException("No estás autenticado.");
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");
        var group = await db.ChatGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new GraphQLException("No se encontró el grupo.");
        var isMember = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct);
        if (!isMember) throw new GraphQLException("No eres miembro de este grupo.");
        return group;
    }

    private static async Task<Profile> RequireMyProfileAsync(
        CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
        => await db.Profiles.FirstOrDefaultAsync(p => p.UserId == CurrentUserId(http), ct)
           ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");

    internal static Guid? CurrentUserId(IHttpContextAccessor http)
        => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    /// <summary>
    /// Nombre para mostrar a partir de los claims del JWT. El Auth Service emite
    /// <see cref="ClaimTypes.Name"/> como "FirstName LastName"; si no está presente
    /// se intenta con GivenName/Surname y finalmente con el Name del identity.
    /// </summary>
    internal static string? DisplayNameFromClaims(IHttpContextAccessor http)
    {
        var user = http.HttpContext?.User;
        if (user is null) return null;
        var name = user.FindFirstValue(ClaimTypes.Name)
                   ?? user.FindFirstValue("name")
                   ?? user.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
        var given = user.FindFirstValue(ClaimTypes.GivenName);
        var surname = user.FindFirstValue(ClaimTypes.Surname);
        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(surname))
            return $"{given} {surname}".Trim();
        return null;
    }
}

