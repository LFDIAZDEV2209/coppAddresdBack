using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoppAddresd.Community.Security;

/// <summary>Identidad del visor y límites de interacción entre dos miembros.</summary>
public static class CommunityBlockPolicy
{
    public static bool IsErpRequest(IHttpContextAccessor http)
        => http.HttpContext?.User.HasClaim("aud", "erp") == true
           && http.HttpContext.User.HasClaim("aud", "app") == false;

    public static async Task InitializeRequestAsync(
        CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        var userId = CommunityQuery.CurrentUserId(http);
        Guid? viewer = null;
        if (userId is not null && !IsErpRequest(http))
            viewer = await db.Profiles.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.UserId == userId && !p.IsSystem)
                .Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct);
        if (http.HttpContext is { } context)
        {
            if (viewer is Guid id) context.Items[CommunityDbContext.VisibilityProfileItem] = id;
            else context.Items.Remove(CommunityDbContext.VisibilityProfileItem);
        }
        db.UseBlockVisibility(viewer);
    }

    public static async Task<Guid?> ViewerProfileIdAsync(
        CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        await InitializeRequestAsync(db, http, ct);
        return db.BlockFilteringEnabled ? db.VisibilityProfileId : null;
    }

    public static async Task<Profile> RequireActorAsync(
        CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        if (http.HttpContext?.User.Identity?.IsAuthenticated != true)
            throw new GraphQLException("No estás autenticado.");
        var userId = CommunityQuery.CurrentUserId(http)
            ?? throw new GraphQLException("No estás autenticado.");
        var profile = await db.Profiles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.UserId == userId && !p.IsSystem, ct)
            ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");
        if (profile.Status != ProfileStatus.Active)
            throw new GraphQLException("Tu perfil está suspendido en la comunidad.");
        return profile;
    }

    public static Task<bool> IsExcludedAsync(
        CommunityDbContext db, Guid viewerId, Guid otherId, CancellationToken ct)
        => db.ProfileBlocks.AnyAsync(b =>
            (b.BlockerProfileId == viewerId && b.BlockedProfileId == otherId)
            || (b.BlockerProfileId == otherId && b.BlockedProfileId == viewerId), ct);

    public static async Task RequirePairAsync(
        CommunityDbContext db, Guid viewerId, Guid otherId, CancellationToken ct)
    {
        if (await IsExcludedAsync(db, viewerId, otherId, ct)) throw Unavailable();
    }

    public static GraphQLException Unavailable()
        => new(ErrorBuilder.New().SetMessage("Este perfil o contenido no está disponible.")
            .SetCode("COMMUNITY_CONTENT_UNAVAILABLE").Build());

    public static async Task RequirePostAsync(
        CommunityDbContext db, Guid viewerId, Guid postId, CancellationToken ct)
    {
        var author = await db.Posts.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == postId && p.DeletedAt == null)
            .Select(p => (Guid?)p.ProfileId).SingleOrDefaultAsync(ct);
        if (author is null) throw Unavailable();
        await RequirePairAsync(db, viewerId, author.Value, ct);
    }

    public static async Task RequireCommentAsync(
        CommunityDbContext db, Guid viewerId, Guid commentId, CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
        {
            var visible = await db.Comments.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.Id == commentId)
                .Select(c => CommunityDbContext.IsCommentThreadVisible(viewerId, c.Id))
                .SingleOrDefaultAsync(ct);
            if (!visible) throw Unavailable();
            return;
        }
        var comment = await db.Comments.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == commentId && c.DeletedAt == null)
            .Select(c => new { c.PostId, c.ProfileId, c.ParentCommentId }).SingleOrDefaultAsync(ct);
        if (comment is null) throw Unavailable();
        await RequirePairAsync(db, viewerId, comment.ProfileId, ct);
        await RequirePostAsync(db, viewerId, comment.PostId, ct);
        if (comment.ParentCommentId is Guid rootId)
        {
            var root = await db.Comments.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.Id == rootId && c.DeletedAt == null)
                .Select(c => (Guid?)c.ProfileId).SingleOrDefaultAsync(ct);
            if (root is null) throw Unavailable();
            await RequirePairAsync(db, viewerId, root.Value, ct);
        }
    }

    /// <summary>Serializa bloquear, seguir y enviar; la publicación de eventos queda fuera de la transacción.</summary>
    public static async Task<IDbContextTransaction?> BeginPairWriteAsync(
        CommunityDbContext db, Guid a, Guid b, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (db.Database.IsNpgsql())
            {
                var pair = new[] { a.ToString("N"), b.ToString("N") }.Order(StringComparer.Ordinal);
                var key = "community-pair:" + string.Join(':', pair);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public static async Task<bool> BlockAsync(
        CommunityDbContext db, IHttpContextAccessor http, Guid targetId, CancellationToken ct)
    {
        var actor = await RequireActorAsync(db, http, ct);
        await ValidateTargetAsync(db, actor.Id, targetId, ct);
        await using var transaction = await BeginPairWriteAsync(db, actor.Id, targetId, ct);
        if (!await db.ProfileBlocks.AnyAsync(b => b.BlockerProfileId == actor.Id && b.BlockedProfileId == targetId, ct))
            db.ProfileBlocks.Add(new ProfileBlock
            {
                Id = Guid.NewGuid(), BlockerProfileId = actor.Id, BlockedProfileId = targetId,
                CreatedAt = DateTime.UtcNow,
            });
        var follows = await db.Follows.IgnoreQueryFilters().Where(f =>
            (f.FollowerProfileId == actor.Id && f.FollowingProfileId == targetId)
            || (f.FollowerProfileId == targetId && f.FollowingProfileId == actor.Id)).ToListAsync(ct);
        db.Follows.RemoveRange(follows);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return true;
    }

    public static async Task<bool> UnblockAsync(
        CommunityDbContext db, IHttpContextAccessor http, Guid targetId, CancellationToken ct)
    {
        var actor = await RequireActorAsync(db, http, ct);
        await ValidateTargetAsync(db, actor.Id, targetId, ct);
        await using var transaction = await BeginPairWriteAsync(db, actor.Id, targetId, ct);
        var rows = await db.ProfileBlocks
            .Where(b => b.BlockerProfileId == actor.Id && b.BlockedProfileId == targetId).ToListAsync(ct);
        db.ProfileBlocks.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return true;
    }

    private static async Task ValidateTargetAsync(
        CommunityDbContext db, Guid actorId, Guid targetId, CancellationToken ct)
    {
        if (actorId == targetId) throw Unavailable();
        if (!await db.Profiles.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(p => p.Id == targetId && !p.IsSystem && p.UserId != null, ct)) throw Unavailable();
    }
}
