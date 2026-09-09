using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Types;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoppAddresd.Community.GraphQL.Queries;

/// <summary>Consultas del módulo de clubes (contrato D1 congelado + ampliación).</summary>
[ExtendObjectType(typeof(CommunityQuery))]
public sealed class ClubQuery
{
    /// <summary>Lista de clubes con filtros opcionales (categoría, búsqueda por nombre/etiquetas, visibilidad).</summary>
    [Authorize]
    public async Task<IReadOnlyList<Club>> Clubs(
        [Service] CommunityDbContext db,
        ClubFilterInput? filter = null,
        int take = 20,
        int skip = 0,
        CancellationToken ct = default)
    {
        var query = db.Clubs
            .AsNoTracking()
            .Where(c => c.Status == ClubStatus.Activo);

        if (!string.IsNullOrWhiteSpace(filter?.Category))
        {
            var category = filter.Category.Trim();
            query = query.Where(c => c.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                c.Description.ToLower().Contains(search) ||
                c.Tags.Any(t => t.ToLower().Contains(search)));
        }

        if (filter?.Visibility is not null)
        {
            query = query.Where(c => c.Visibility == filter.Visibility.Value);
        }

        var clubs = await query
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(Math.Min(take, 100))
            .ToListAsync(ct);

        return await FillMemberCountsAsync(db, clubs, ct);
    }

    /// <summary>Ficha completa de un club.</summary>
    [Authorize]
    public async Task<Club?> Club(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var club = await db.Clubs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (club is null) return null;

        var count = await db.ClubMembers.CountAsync(m => m.ClubId == id && m.Status == ClubMemberStatus.Activo, ct);
        club.MemberCount = count;
        return club;
    }

    /// <summary>
    /// Feed del club: solo publicaciones PUBLICADO, pin primero y luego fecha
    /// descendente (keyset-ready por índice (club_id, created_at)). Si el
    /// solicitante no es miembro, solo ve posts de visibilidad Público.
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<Post>> ClubFeed(
        Guid clubId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        int take = 20,
        int skip = 0,
        CancellationToken ct = default)
    {
        var myId = CommunityQuery.CurrentUserId(http);
        var isManager = http.HttpContext?.User?.Claims.Any(c =>
            c.Type == "permission" && c.Value.Equals("Community.Manage", StringComparison.OrdinalIgnoreCase)) == true;
        var myProfileId = myId is null
            ? Guid.Empty
            : await db.Profiles.Where(p => p.UserId == myId).Select(p => p.Id).FirstOrDefaultAsync(ct);
        var isMember = isManager || (myProfileId != Guid.Empty && await db.ClubMembers.AnyAsync(
            m => m.ClubId == clubId && m.ProfileId == myProfileId && m.Status == ClubMemberStatus.Activo, ct));

        var query = db.Posts
            .AsNoTracking()
            .Where(p => p.ClubId == clubId && p.ClubStatus == ClubPostStatus.Publicado && p.DeletedAt == null);

        if (!isMember)
        {
            query = query.Where(p => p.ClubVisibility == ClubPostVisibility.Publico);
        }

        var posts = await query
            .OrderByDescending(p => p.Pinned)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(take, 100))
            .Include(p => p.Profile)
            .Include(p => p.Likes)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Likes)
            .Include(p => p.Poll).ThenInclude(poll => poll.Options).ThenInclude(o => o.Votes)
            .ToListAsync(ct);

        return posts;
    }

    /// <summary>Posts públicos de clubes públicos para el feed social general ("Para ti").</summary>
    [Authorize]
    public async Task<IReadOnlyList<Post>> PublicClubPosts(
        [Service] CommunityDbContext db,
        int take = 5,
        CancellationToken ct = default)
        => await db.Posts
            .AsNoTracking()
            .Where(p => p.ClubId != null
                && p.ClubStatus == ClubPostStatus.Publicado
                && p.ClubVisibility == ClubPostVisibility.Publico
                && p.DeletedAt == null
                && p.Club!.Status == ClubStatus.Activo
                && p.Club.Visibility == ClubVisibility.Publico)
            .OrderByDescending(p => p.CreatedAt)
            .Take(Math.Min(take, 10))
            .Include(p => p.Club)
            .Include(p => p.Profile)
            .Include(p => p.Likes)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Poll).ThenInclude(poll => poll.Options).ThenInclude(o => o.Votes)
            .ToListAsync(ct);

    /// <summary>Eventos del club ordenados por inicio (próximos primero).</summary>
    [Authorize]
    public async Task<IReadOnlyList<ClubEvent>> ClubEvents(
        Guid clubId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => await db.ClubEvents
            .AsNoTracking()
            .Where(e => e.ClubId == clubId)
            .OrderBy(e => e.StartsAt)
            .ToListAsync(ct);

    /// <summary>Sesiones en vivo del club ordenadas por inicio.</summary>
    [Authorize]
    public async Task<IReadOnlyList<LiveSession>> ClubLiveSessions(
        Guid clubId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => await db.LiveSessions
            .AsNoTracking()
            .Where(l => l.ClubId == clubId)
            .OrderBy(l => l.ScheduledStartAt)
            .Include(l => l.Speakers).ThenInclude(s => s.Profile)
            .ToListAsync(ct);

    /// <summary>Analítica derivada de un club (miembros, crecimiento, engagement, top posts).</summary>
    [Authorize]
    public async Task<ClubAnalyticsDto?> ClubAnalytics(
        Guid clubId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var exists = await db.Clubs.AnyAsync(c => c.Id == clubId, ct);
        if (!exists) return null;

        var activeMembers = await db.ClubMembers.CountAsync(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Activo, ct);

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var twoWeeksAgo = now.AddDays(-14);

        var thisWeek = await db.ClubMembers.CountAsync(m => m.ClubId == clubId && m.JoinedAt >= weekAgo, ct);
        var lastWeek = await db.ClubMembers.CountAsync(m => m.ClubId == clubId && m.JoinedAt >= twoWeeksAgo && m.JoinedAt < weekAgo, ct);
        var weeklyGrowth = lastWeek == 0 ? (thisWeek > 0 ? 100 : 0) : (int)Math.Round((thisWeek - lastWeek) * 100d / lastWeek);

        var postsThisWeek = await db.Posts.CountAsync(p => p.ClubId == clubId && p.CreatedAt >= weekAgo && p.DeletedAt == null, ct);
        var engagement = activeMembers == 0 ? 0 : Math.Round(postsThisWeek * 100d / activeMembers, 1);

        var topPosts = await db.Posts
            .AsNoTracking()
            .Where(p => p.ClubId == clubId && p.DeletedAt == null)
            .OrderByDescending(p => p.Likes.Count)
            .Take(5)
            .Include(p => p.Profile)
            .ToListAsync(ct);

        var retention = activeMembers == 0
            ? 0
            : Math.Round(await db.ClubMembers.CountAsync(m => m.ClubId == clubId && m.JoinedAt <= twoWeeksAgo && m.Status == ClubMemberStatus.Activo, ct) * 100d / activeMembers, 1);

        var events = await db.ClubEvents
            .AsNoTracking()
            .Where(e => e.ClubId == clubId)
            .OrderByDescending(e => e.StartsAt)
            .Take(5)
            .ToListAsync(ct);

        return new ClubAnalyticsDto(activeMembers, weeklyGrowth, engagement, topPosts, retention, events);
    }

    /// <summary>Miembros del club (filtro opcional por estado; por defecto solo Activos).</summary>
    [Authorize]
    public async Task<IReadOnlyList<ClubMember>> ClubMembers(
        Guid clubId,
        [Service] CommunityDbContext db,
        ClubMemberStatus? status = null,
        CancellationToken ct = default)
        => await db.ClubMembers
            .AsNoTracking()
            .Where(m => m.ClubId == clubId && (status == null || m.Status == status))
            .OrderBy(m => m.JoinedAt)
            .Include(m => m.Profile)
            .ToListAsync(ct);

    /// <summary>Solicitudes pendientes de ingreso al club.</summary>
    [Authorize]
    public async Task<IReadOnlyList<ClubMember>> ClubRequests(
        Guid clubId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => await db.ClubMembers
            .AsNoTracking()
            .Where(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Pendiente)
            .Include(m => m.Profile)
            .ToListAsync(ct);

    /// <summary>Valida una invitación por token (sin consumirla).</summary>
    [Authorize]
    public async Task<ClubInvitation?> ClubInvitation(
        string token,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => await db.ClubInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token && i.UsedAt == null && i.ExpiresAt > DateTime.UtcNow, ct);

    /// <summary>Clubes a los que pertenece el usuario autenticado (membresía Activa).</summary>
    [Authorize]
    public async Task<IReadOnlyList<Club>> MyClubs(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var myId = CommunityQuery.CurrentUserId(http);
        if (myId is null) return [];

        var profileId = await db.Profiles.Where(p => p.UserId == myId).Select(p => p.Id).FirstOrDefaultAsync(ct);
        if (profileId == Guid.Empty) return [];

        var clubs = await db.Clubs
            .AsNoTracking()
            .Where(c => c.Members.Any(m => m.ProfileId == profileId && m.Status == ClubMemberStatus.Activo))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return await FillMemberCountsAsync(db, clubs, ct);
    }

    /// <summary>Notificaciones in-app de clubes del usuario autenticado (no leídas primero).</summary>
    [Authorize]
    public async Task<IReadOnlyList<ClubNotification>> ClubNotifications(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        int take = 50,
        CancellationToken ct = default)
    {
        var myId = CommunityQuery.CurrentUserId(http);
        if (myId is null) return [];

        var profileId = await db.Profiles.Where(p => p.UserId == myId).Select(p => p.Id).FirstOrDefaultAsync(ct);
        if (profileId == Guid.Empty) return [];

        return await db.ClubNotifications
            .AsNoTracking()
            .Where(n => n.ProfileId == profileId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Min(take, 100))
            .Include(n => n.Club)
            .ToListAsync(ct);
    }

    /// <summary>Reportes de moderación del club (posts y comentarios de sus publicaciones).</summary>
    [Authorize]
    public async Task<IReadOnlyList<ModerationReportDto>> ClubReports(
        Guid clubId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var postReports = await db.PostReports
            .AsNoTracking()
            .Where(r => r.Post!.ClubId == clubId)
            .Include(r => r.Post)
            .ToListAsync(ct);

        var commentReports = await db.CommentReports
            .AsNoTracking()
            .Where(r => r.Comment!.Post.ClubId == clubId)
            .Include(r => r.Comment).ThenInclude(c => c.Post)
            .ToListAsync(ct);

        var result = postReports.Select(r => new ModerationReportDto(
                r.Id, clubId, "POST", r.PostId, r.ReportedByProfileId ?? Guid.Empty, r.Reason, "PENDIENTE", r.CreatedAt))
            .Concat(commentReports.Select(r => new ModerationReportDto(
                r.Id, clubId, "COMENTARIO", r.CommentId, r.ReportedByProfileId ?? Guid.Empty, r.Reason, "PENDIENTE", r.CreatedAt)))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return result;
    }

    private static async Task<IReadOnlyList<Club>> FillMemberCountsAsync(CommunityDbContext db, List<Club> clubs, CancellationToken ct)
    {
        if (clubs.Count == 0) return clubs;

        var ids = clubs.Select(c => c.Id).ToList();
        var counts = await db.ClubMembers
            .Where(m => ids.Contains(m.ClubId) && m.Status == ClubMemberStatus.Activo)
            .GroupBy(m => m.ClubId)
            .Select(g => new { ClubId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClubId, x => x.Count, ct);

        foreach (var club in clubs)
        {
            club.MemberCount = counts.GetValueOrDefault(club.Id);
        }

        return clubs;
    }
}