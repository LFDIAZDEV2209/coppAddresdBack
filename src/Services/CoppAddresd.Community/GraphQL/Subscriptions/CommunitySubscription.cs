using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using CoppAddresd.Community.Security;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Subscriptions;

/// <summary>Suscripciones de la comunidad con visibilidad reevaluada por evento.</summary>
[Authorize]
public sealed class CommunitySubscription
{
    [Subscribe(With = nameof(SubscribeToMessageAdded))]
    [Topic("message_{conversationKey}")]
    public Message MessageAdded(string conversationKey, [EventMessage] Message message) => message;

    [Subscribe(With = nameof(SubscribeToPostAdded))]
    [Topic("post_added")]
    public Post PostAdded([EventMessage] Post post) => post;

    [Subscribe(With = nameof(SubscribeToGroupMessageAdded))]
    [Topic("group_{groupId}")]
    public Message GroupMessageAdded(Guid groupId, [EventMessage] Message message) => message;

    [Subscribe(With = nameof(SubscribeToFeedEventAdded))]
    [Topic("feed_event_added")]
    public FeedEvent FeedEventAdded([EventMessage] FeedEvent feedEvent) => feedEvent;

    [Subscribe(With = nameof(SubscribeToCommentAdded))]
    [Topic("comment_added")]
    public Comment CommentAdded([EventMessage] Comment comment) => comment;

    [Subscribe(With = nameof(SubscribeToGroupChanged))]
    [Topic("group_{groupId}_changed")]
    public ChatGroup GroupChanged(Guid groupId, [EventMessage] ChatGroup group) => group;

    public async ValueTask<ISourceStream<Post>> SubscribeToPostAdded(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        var moderator = BlockingEventStream.IsErpModerator(http);
        return await BlockingEventStream.SubscribeAsync<Post>(receiver, "post_added", scopeFactory, viewerId,
            (queued, fresh, token) => ReloadPostAsync(queued.Id, fresh, viewerId, moderator, token), ct);
    }

    public async ValueTask<ISourceStream<FeedEvent>> SubscribeToFeedEventAdded(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        return await BlockingEventStream.SubscribeAsync<FeedEvent>(receiver, "feed_event_added", scopeFactory, viewerId,
            (queued, fresh, token) => fresh.FeedEvents.Include(f => f.Profile)
                .FirstOrDefaultAsync(f => f.Id == queued.Id, token), ct);
    }

    public async ValueTask<ISourceStream<Comment>> SubscribeToCommentAdded(
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        var moderator = BlockingEventStream.IsErpModerator(http);
        return await BlockingEventStream.SubscribeAsync<Comment>(receiver, "comment_added", scopeFactory, viewerId,
            async (queued, fresh, token) =>
            {
                var comment = await fresh.Comments.AsSplitQuery()
                    .Include(c => c.Profile).Include(c => c.Likes).Include(c => c.Post)
                    .Include(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Profile)
                    .Include(c => c.Replies.Where(r => r.DeletedAt == null)).ThenInclude(r => r.Likes)
                    .FirstOrDefaultAsync(c => c.Id == queued.Id && c.DeletedAt == null
                        && c.Post!.DeletedAt == null, token);
                return comment?.Post is { } post
                    && await BlockingEventStream.CanViewPostAsync(fresh, post, viewerId, moderator, token)
                    ? comment : null;
            }, ct);
    }

    /// <summary>La membresía propia se verifica al abrir el stream y antes de cada mensaje.</summary>
    public async ValueTask<ISourceStream<Message>> SubscribeToGroupMessageAdded(
        Guid groupId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var actorId = (await CommunityBlockPolicy.RequireActorAsync(db, http, ct)).Id;
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        if (!await IsGroupMemberAsync(db, groupId, actorId, ct))
            throw new GraphQLException("No eres miembro de este grupo.");
        return await BlockingEventStream.SubscribeAsync<Message>(receiver, $"group_{groupId}", scopeFactory, viewerId,
            async (queued, fresh, token) => await IsGroupMemberAsync(fresh, groupId, actorId, token)
                ? await fresh.Messages.FirstOrDefaultAsync(m => m.Id == queued.Id && m.ConversationId == groupId, token)
                : null, ct);
    }

    public async ValueTask<ISourceStream<ChatGroup>> SubscribeToGroupChanged(
        Guid groupId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var actorId = (await CommunityBlockPolicy.RequireActorAsync(db, http, ct)).Id;
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        if (!await IsGroupMemberAsync(db, groupId, actorId, ct))
            throw new GraphQLException("No eres miembro de este grupo.");
        return await BlockingEventStream.SubscribeAsync<ChatGroup>(receiver, $"group_{groupId}_changed", scopeFactory, viewerId,
            async (queued, fresh, token) =>
            {
                if (queued.Id != groupId || !await IsGroupMemberAsync(fresh, groupId, actorId, token))
                    return null;
                var group = await fresh.ChatGroups.AsSplitQuery()
                    .Include(g => g.Members).ThenInclude(m => m.Profile)
                    .FirstOrDefaultAsync(g => g.Id == groupId, token);
                if (group is not null)
                {
                    group.MemberCount = group.Members.Count;
                    group.LastMessage = await fresh.Messages.Where(m => m.ConversationId == groupId)
                        .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).FirstOrDefaultAsync(token);
                }
                return group;
            }, ct);
    }

    /// <summary>La clave pública del tópico no concede acceso a conversaciones ajenas.</summary>
    public async ValueTask<ISourceStream<Message>> SubscribeToMessageAdded(
        string conversationKey,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var actorId = await EnsureParticipantAsync(conversationKey, db, http, ct);
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        TryParseConversationKey(conversationKey, out var a, out var b);
        return await BlockingEventStream.SubscribeAsync<Message>(receiver, $"message_{conversationKey}", scopeFactory, viewerId,
            async (queued, fresh, token) =>
            {
                if (!await fresh.Profiles.AnyAsync(p => p.Id == actorId, token)
                    || !await fresh.Profiles.AnyAsync(p => p.Id == (actorId == a ? b : a), token))
                    return null;
                return await fresh.Messages.FirstOrDefaultAsync(m => m.Id == queued.Id && m.ConversationId == null
                    && ((m.SenderProfileId == a && m.RecipientProfileId == b)
                        || (m.SenderProfileId == b && m.RecipientProfileId == a)), token);
            }, ct);
    }

    internal static async Task<Post?> ReloadPostAsync(
        Guid id, CommunityDbContext db, Guid? viewerId, bool moderator, CancellationToken ct)
    {
        var post = await new CommunityQuery().Post(id, db, ct);
        return post is not null && await BlockingEventStream.CanViewPostAsync(db, post, viewerId, moderator, ct)
            ? post : null;
    }

    private static Task<bool> IsGroupMemberAsync(CommunityDbContext db, Guid groupId, Guid actorId, CancellationToken ct)
        => db.ChatGroupMembers.AnyAsync(m => m.GroupId == groupId && m.ProfileId == actorId, ct);

    internal static async Task<Guid> EnsureParticipantAsync(
        string conversationKey, CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        var profile = await CommunityBlockPolicy.RequireActorAsync(db, http, ct);
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        db.UseBlockVisibility(viewerId);
        if (!TryParseConversationKey(conversationKey, out var a, out var b) || (profile.Id != a && profile.Id != b)
            || !await db.Profiles.AnyAsync(p => p.Id == (profile.Id == a ? b : a), ct))
            throw new GraphQLException("No puedes suscribirte a esta conversación.");
        return profile.Id;
    }

    /// <summary>Interpreta una clave de conversación "idA:idB" como dos GUIDs.</summary>
    internal static bool TryParseConversationKey(string key, out Guid a, out Guid b)
    {
        a = default;
        b = default;
        var parts = key.Split(':');
        return parts.Length == 2 && Guid.TryParse(parts[0], out a) && Guid.TryParse(parts[1], out b);
    }
}
