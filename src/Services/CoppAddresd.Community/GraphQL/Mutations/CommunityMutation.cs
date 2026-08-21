using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Mutations;

[Authorize]
public sealed class CommunityMutation
{
    // --- Perfiles ---

    public async Task<Profile> UpdateProfile(
        string displayName,
        string? bio,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CommunityQuery.CurrentUserId(http);
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new GraphQLException("No tienes perfil en la comunidad.");

        profile.DisplayName = displayName;
        profile.Bio = bio;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return profile;
    }

    // --- Publicaciones ---

    public async Task<Post> CreatePost(
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        return post;
    }

    public async Task<Post?> DeletePost(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("No encontraste esta publicación.");
        post.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return post;
    }

    // --- Likes ---

    public async Task<Post?> LikePost(
        Guid postId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var exists = await db.Likes.AnyAsync(l => l.PostId == postId && l.ProfileId == profile.Id, ct);
        if (!exists)
        {
            db.Likes.Add(new Like { Id = Guid.NewGuid(), PostId = postId, ProfileId = profile.Id });
            await db.SaveChangesAsync(ct);
        }
        return await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
    }

    public async Task<Post?> UnlikePost(
        Guid postId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var like = await db.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.ProfileId == profile.Id, ct);
        if (like is not null)
        {
            db.Likes.Remove(like);
            await db.SaveChangesAsync(ct);
        }
        return await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
    }

    // --- Comentarios ---

    public async Task<Comment> AddComment(
        Guid postId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ProfileId = profile.Id,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        return comment;
    }

    public async Task<Comment> ReplyToComment(
        Guid commentId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var parent = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new GraphQLException("No encontraste el comentario.");
        var reply = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = parent.PostId,
            ProfileId = profile.Id,
            ParentCommentId = parent.Id,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.Comments.Add(reply);
        await db.SaveChangesAsync(ct);
        return reply;
    }

    public async Task<Comment?> DeleteComment(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == id && c.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("No encontraste el comentario.");
        comment.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return comment;
    }

    // --- Moderación (admin) ---

    [Authorize(Policy = "CommunityModerator")]
    public async Task<Profile?> ApproveProfile(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new GraphQLException("No se encontró el perfil.");
        profile.Status = ProfileStatus.Approved;
        profile.ReviewedBy = CommunityQuery.CurrentUserId(http);
        profile.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return profile;
    }

    [Authorize(Policy = "CommunityModerator")]
    public async Task<Profile?> RejectProfile(
        Guid id,
        string? reason,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new GraphQLException("No se encontró el perfil.");
        profile.Status = ProfileStatus.Rejected;
        profile.ReviewedBy = CommunityQuery.CurrentUserId(http);
        profile.ReviewedAt = DateTime.UtcNow;
        profile.RejectionReason = reason;
        await db.SaveChangesAsync(ct);
        return profile;
    }

    [Authorize(Policy = "CommunityModerator")]
    public async Task<Post?> PinPost(
        Guid id,
        bool pinned,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");
        post.Pinned = pinned;
        await db.SaveChangesAsync(ct);
        return post;
    }

    private static async Task<Profile> RequireProfileAsync(
        CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        var userId = CommunityQuery.CurrentUserId(http);
        return await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");
    }
}

