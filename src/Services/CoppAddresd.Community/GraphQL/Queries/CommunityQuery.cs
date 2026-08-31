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
                .ThenInclude(x => x.Reposts)
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
                .ThenInclude(x => x.Reposts)
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
            .Include(p => p.Reposts)
            .Include(p => p.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes)
            .Include(p => p.Comments)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Likes)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Likes)
.Include(p => p.Comments.Where(c => c.DeletedAt == null))
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Profile)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Likes)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Profile)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Likes)
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
            .ThenBy(p => p.PinnedOrder)
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
            .Include(p => p.Reposts)
            .Include(p => p.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes)
            .Include(p => p.Comments)
            .ThenInclude(c => c.Replies)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Likes)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Likes)
.Include(p => p.Comments.Where(c => c.DeletedAt == null))
            .ThenInclude(c => c.Replies.Where(r => r.DeletedAt == null))
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Profile)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Likes)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Profile)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Likes)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);

    /// <summary>Lista de perfiles con filtros opcionales por estado y búsqueda (moderador).</summary>
    [Authorize(Policy = "CommunityModerator")]
    public Task<List<Profile>> Profiles(
        ProfileStatus? status,
        string? search,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var currentUserId = CurrentUserId(http);
        var query = db.Profiles.AsQueryable();
        // El ERP no lista al perfil sistema (Equipo ANTARES) ni al propio admin.
        query = query.Where(p => !p.IsSystem && (currentUserId == null || p.UserId != currentUserId));
        if (status is not null) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(EF.Functions.Unaccent(p.DisplayName), EF.Functions.Unaccent($"%{search}%")));
        return query
            .OrderBy(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Publicaciones reportadas con sus reportes asociados. Solo moderadores.
    /// Consultas secuenciales (EF Core no permite operaciones concurrentes sobre un mismo DbContext).
    /// </summary>
    [Authorize(Policy = "CommunityModerator")]
    public async Task<List<ReportedPost>> ReportedPosts(
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 20,
        int skip = 0)
    {
        // 1. Obtener los PostIds únicos con reportes, ordenados por el reporte más reciente.
        var reportedPostIds = await db.PostReports
            .GroupBy(r => r.PostId)
            .Select(g => new { PostId = g.Key, LatestReportAt = g.Max(r => r.CreatedAt) })
            .OrderByDescending(x => x.LatestReportAt)
            .Skip(skip)
            .Take(take)
            .Select(x => x.PostId)
            .ToListAsync(ct);

        if (reportedPostIds.Count == 0) return [];

        // 2. Cargar las publicaciones activas (no eliminadas) correspondientes.
        var posts = await db.Posts
            .Where(p => reportedPostIds.Contains(p.Id) && p.DeletedAt == null)
            .Include(p => p.Profile)
            .Include(p => p.Likes)
            .ToListAsync(ct);
        var postById = posts.ToDictionary(p => p.Id);

        // 3. Cargar los reportes de esas publicaciones.
        var reports = await db.PostReports
            .Where(r => reportedPostIds.Contains(r.PostId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        // 4. Cargar perfiles de reportadores.
        var reporterIds = reports.Select(r => r.ReportedByProfileId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var reporters = await db.Profiles.Where(p => reporterIds.Contains(p.Id)).ToListAsync(ct);
        var reporterById = reporters.ToDictionary(p => p.Id);

        // 5. Agrupar y construir DTOs en el orden original.
        var reportsByPostId = reports.GroupBy(r => r.PostId).ToDictionary(g => g.Key, g => g.ToList());

        return reportedPostIds
            .Where(id => postById.ContainsKey(id))
            .Select(id =>
            {
                var post = postById[id];
                var postReports = reportsByPostId.GetValueOrDefault(id, []);
                return new ReportedPost
                {
                    PostId = id,
                    Post = post,
                    ReportCount = postReports.Count,
                    Reports = postReports.Select(r => new ReportDto
                    {
                        Id = r.Id,
                        PostId = r.PostId,
                        Reason = r.Reason,
                        Details = r.Details,
                        CreatedAt = r.CreatedAt,
                        ReportedBy = r.ReportedByProfileId.HasValue && reporterById.TryGetValue(r.ReportedByProfileId.Value, out var reporter)
                            ? new ReportedByProfile { Id = reporter.Id, DisplayName = reporter.DisplayName }
                            : null,
                    }).ToList(),
                };
            })
            .ToList();
    }

    /// <summary>
    /// Comentarios reportados con sus reportes asociados. Solo moderadores.
    /// Consultas secuenciales (EF Core no permite operaciones concurrentes sobre un mismo DbContext).
    /// </summary>
    [Authorize(Policy = "CommunityModerator")]
    public async Task<List<ReportedComment>> ReportedComments(
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 20,
        int skip = 0)
    {
        // 1. Obtener los CommentIds únicos con reportes, ordenados por el reporte más reciente.
        var reportedCommentIds = await db.CommentReports
            .GroupBy(r => r.CommentId)
            .Select(g => new { CommentId = g.Key, LatestReportAt = g.Max(r => r.CreatedAt) })
            .OrderByDescending(x => x.LatestReportAt)
            .Skip(skip)
            .Take(take)
            .Select(x => x.CommentId)
            .ToListAsync(ct);

        if (reportedCommentIds.Count == 0) return [];

        // 2. Cargar los comentarios activos (no eliminados) correspondientes, con su Post padre.
        var comments = await db.Comments
            .Where(c => reportedCommentIds.Contains(c.Id) && c.DeletedAt == null)
            .Include(c => c.Profile)
            .Include(c => c.Likes)
            .Include(c => c.Post)
            .ToListAsync(ct);
        var commentById = comments.ToDictionary(c => c.Id);

        // 3. Cargar los reportes de esos comentarios.
        var reports = await db.CommentReports
            .Where(r => reportedCommentIds.Contains(r.CommentId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        // 4. Cargar perfiles de reportadores.
        var reporterIds = reports.Select(r => r.ReportedByProfileId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var reporters = await db.Profiles.Where(p => reporterIds.Contains(p.Id)).ToListAsync(ct);
        var reporterById = reporters.ToDictionary(p => p.Id);

        // 5. Agrupar y construir DTOs en el orden original.
        var reportsByCommentId = reports.GroupBy(r => r.CommentId).ToDictionary(g => g.Key, g => g.ToList());

        return reportedCommentIds
            .Where(id => commentById.ContainsKey(id))
            .Select(id =>
            {
                var comment = commentById[id];
                var commentReports = reportsByCommentId.GetValueOrDefault(id, []);
                return new ReportedComment
                {
                    CommentId = id,
                    Comment = comment,
                    Post = comment.Post,
                    ReportCount = commentReports.Count,
                    Reports = commentReports.Select(r => new CommentReportDto
                    {
                        Id = r.Id,
                        CommentId = r.CommentId,
                        Reason = r.Reason,
                        Details = r.Details,
                        CreatedAt = r.CreatedAt,
                        ReportedBy = r.ReportedByProfileId.HasValue && reporterById.TryGetValue(r.ReportedByProfileId.Value, out var reporter)
                            ? new ReportedByProfile { Id = reporter.Id, DisplayName = reporter.DisplayName }
                            : null,
                    }).ToList(),
                };
            })
            .ToList();
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
            .Include(p => p.Reposts)
            .Include(p => p.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes)
            .Include(p => p.Comments)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Likes)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Likes)
.Include(p => p.Comments.Where(c => c.DeletedAt == null))
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Profile)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Likes)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Profile)
            .Include(p => p.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Likes)
            .Where(p => p.DeletedAt == null && followingIds.Contains(p.ProfileId))
            .OrderByDescending(p => p.Pinned)
            .ThenBy(p => p.PinnedOrder)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Perfiles que repostearon una publicación (orden por fecha de repost, más reciente primero).</summary>
    [Authorize]
    public async Task<List<Profile>> PostReposts(
        Guid postId,
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var ids = await db.Reposts
            .Where(r => r.PostId == postId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.ProfileId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        var loaded = await db.Profiles.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var byId = loaded.ToDictionary(p => p.Id);
        return ids.Select(id => byId[id]).ToList();
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

    /// <summary>
    /// Estadísticas agregadas del dashboard de la comunidad: KPIs, series temporales,
    /// distribuciones y tendencias. Consultas encadenadas secuenciales (EF Core no permite
    /// operaciones concurrentes sobre un mismo DbContext).
    /// </summary>
    [Authorize(Policy = "Community.View")]
    public async Task<DashboardStats> DashboardStats(
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Carga secuencial de datos (sin operaciones concurrentes en el mismo DbContext).
        var profiles = await db.Profiles.Where(p => !p.IsSystem).ToListAsync(ct);
        var posts = await db.Posts.ToListAsync(ct);
        var comments = await db.Comments.ToListAsync(ct);
        var likes = await db.Likes.ToListAsync(ct);
        var feedEvents = await db.FeedEvents.ToListAsync(ct);

        return DashboardAggregator.Compute(profiles, posts, comments, likes, feedEvents, now);
    }

    // ─── Analytics (nuevas consultas) ──────────────────────────────────

    /// <summary>Estadísticas por región: miembros activos no-sistema y posts por semana.</summary>
    [Authorize(Policy = "Community.View")]
    public async Task<IReadOnlyList<RegionStat>> RegionStats(
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.Profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem && p.Region != null)
            .ToListAsync(ct);
        var posts = await db.Posts.ToListAsync(ct);
        return AnalyticsAggregator.ComputeRegionStats(profiles, posts, now);
    }

    /// <summary>Estadísticas por diagnóstico: miembros, posts/semana, promedio racha/XP, adherencia.</summary>
    [Authorize(Policy = "Community.View")]
    public async Task<IReadOnlyList<DiagnosticStat>> DiagnosticStats(
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.Profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem && p.Diagnosis != null)
            .ToListAsync(ct);
        var posts = await db.Posts.ToListAsync(ct);
        return AnalyticsAggregator.ComputeDiagnosticStats(profiles, posts, now);
    }

    /// <summary>Analytics completo del dashboard: feed hoy, overview rachas, inactividad, series XP.</summary>
    [Authorize(Policy = "Community.View")]
    public async Task<CommunityAnalytics> CommunityAnalytics(
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.Profiles.Where(p => p.Status == ProfileStatus.Active && !p.IsSystem).ToListAsync(ct);
        var posts = await db.Posts.ToListAsync(ct);
        var comments = await db.Comments.ToListAsync(ct);
        var likes = await db.Likes.ToListAsync(ct);
        var feedEvents = await db.FeedEvents.ToListAsync(ct);
        var xpEntries = await db.XpEntries.ToListAsync(ct);

        return new CommunityAnalytics
        {
            FeedToday = AnalyticsAggregator.ComputeFeedToday(profiles, posts, comments, likes, xpEntries, now),
            StreakOverview = AnalyticsAggregator.ComputeStreakOverview(profiles, feedEvents, now),
            InactivityDistribution = AnalyticsAggregator.ComputeInactivityDistribution(profiles, now),
            XpDeliveredSeries = AnalyticsAggregator.ComputeXpDeliveredSeries(xpEntries, now),
        };
    }

    /// <summary>Lista de reconocimientos con perfil (ordenados por CreatedAt descendente).</summary>
    [Authorize(Policy = "Community.View")]
    public async Task<IReadOnlyList<RecognitionDto>> Recognitions(
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var recognitions = await db.Recognitions
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        var profileIds = recognitions.Select(r => r.ProfileId).Distinct().ToList();
        var profiles = await db.Profiles.Where(p => profileIds.Contains(p.Id)).ToListAsync(ct);
        var profileById = profiles.ToDictionary(p => p.Id);

        return recognitions.Select(r =>
        {
            profileById.TryGetValue(r.ProfileId, out var profile);
            return new RecognitionDto
            {
                Id = r.Id,
                ProfileId = r.ProfileId,
                TypeLabel = r.TypeLabel,
                Xp = r.Xp,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt,
                Profile = profile is not null
                    ? new RecognitionProfile
                    {
                        Id = profile.Id,
                        DisplayName = profile.DisplayName,
                        IsSystem = profile.IsSystem,
                    }
                    : null,
            };
        }).ToList();
    }

    /// <summary>Canales de red social con puntos de crecimiento (ordenados por SortOrder).</summary>
    [Authorize(Policy = "Community.View")]
    public async Task<IReadOnlyList<NetworkChannel>> Networks(
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        return await db.NetworkChannels
            .Include(c => c.GrowthPoints)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Todos los grupos de chat: id, name, memberCount, messageCount, lastActivityAt.
    /// Ordenados por messageCount descendente, luego nombre.
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<GroupSummary>> CommunityGroups(
        [Service] CommunityDbContext db,
        CancellationToken ct,
        int take = 50,
        int skip = 0)
    {
        var groups = await db.ChatGroups.ToListAsync(ct);
        var groupIds = groups.Select(g => g.Id).ToList();

        // memberCount por grupo
        var memberCounts = await db.ChatGroupMembers
            .Where(m => groupIds.Contains(m.GroupId))
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var memberCountById = memberCounts.ToDictionary(x => x.GroupId, x => x.Count);

        // messageCount y lastActivityAt por grupo
        var messageStats = await db.Messages
            .Where(m => m.ConversationId != null && groupIds.Contains(m.ConversationId.Value))
            .GroupBy(m => m.ConversationId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count(), LastAt = g.Max(m => m.CreatedAt) })
            .ToListAsync(ct);
        var messageStatsById = messageStats.ToDictionary(x => x.GroupId, x => x);

        return groups
            .Select(g =>
            {
                messageStatsById.TryGetValue(g.Id, out var stats);
                return new GroupSummary
                {
                    Id = g.Id,
                    Name = g.Name,
                    MemberCount = memberCountById.GetValueOrDefault(g.Id, 0),
                    MessageCount = stats?.Count ?? 0,
                    LastActivityAt = stats?.LastAt ?? new DateTimeOffset(g.CreatedAt, TimeSpan.Zero),
                };
            })
            .OrderByDescending(g => g.MessageCount)
            .ThenBy(g => g.Name)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// Alcance de mensajes del sistema: TODOS, INACTIVOS, ACTIVOS7 con totales y alcanzados.
    /// </summary>
    [Authorize(Policy = "Community.View")]
    public async Task<IReadOnlyList<MessageReach>> MessageReach(
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.Profiles.Where(p => p.Status == ProfileStatus.Active && !p.IsSystem).ToListAsync(ct);
        var messages = await db.Messages.ToListAsync(ct);
        return AnalyticsAggregator.ComputeMessageReach(profiles, messages, now);
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

    /// <summary>
    /// Eventos del feed en vivo ordenados por fecha de creación descendente.
    /// </summary>
    [Authorize(Policy = "Community.View")]
    public async Task<IReadOnlyList<FeedEvent>> FeedEvents(
        [Service] CommunityDbContext db,
        int take = 20,
        int skip = 0,
        CancellationToken ct = default)
        => await db.FeedEvents
            .Include(f => f.Profile)
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <summary>
    /// Ranking de perfiles por racha actual (descendente) y XP total (descendente).
    /// Usado por el tablero de rachas del frontend.
    /// </summary>
    [Authorize(Policy = "Community.View")]
    public async Task<List<Profile>> TopStreaks(
        [Service] CommunityDbContext db,
        int take = 20,
        CancellationToken ct = default)
        => await db.Profiles
            .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
            .OrderByDescending(p => p.CurrentStreak)
            .ThenByDescending(p => p.XpTotal)
            .Take(take)
            .ToListAsync(ct);

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

