using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using CoppAddresd.Community.Security;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Subscriptions;

/// <summary>Suscripciones de clubes con autorización inicial y por evento.</summary>
[ExtendObjectType(typeof(CommunitySubscription))]
public sealed class ClubSubscription
{
    [Subscribe(With = nameof(SubscribeToClubPostAdded))]
    [Topic("club_{clubId}_posts")]
    [Authorize]
    public Post ClubPostAdded(Guid clubId, [EventMessage] Post post) => post;

    public async ValueTask<ISourceStream<Post>> SubscribeToClubPostAdded(
        Guid clubId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        var moderator = BlockingEventStream.IsErpModerator(http);
        db.UseBlockVisibility(viewerId);
        if (!await BlockingEventStream.CanViewClubAsync(db, clubId, viewerId, moderator, false, ct))
            throw new GraphQLException("No tienes acceso a este club.");
        return await BlockingEventStream.SubscribeAsync<Post>(receiver, $"club_{clubId}_posts", scopeFactory, viewerId,
            async (queued, fresh, token) =>
            {
                var post = await CommunitySubscription.ReloadPostAsync(queued.Id, fresh, viewerId, moderator, token);
                return post?.ClubId == clubId ? post : null;
            }, ct);
    }

    /// <summary>El tópico personal solo puede pertenecer al perfil del JWT.</summary>
    [Subscribe(With = nameof(SubscribeToClubNotificationAdded))]
    [Topic("club_notif_{profileId}")]
    [Authorize]
    public ClubNotification ClubNotificationAdded(Guid profileId, [EventMessage] ClubNotification notification)
        => notification;

    public async ValueTask<ISourceStream<ClubNotification>> SubscribeToClubNotificationAdded(
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        await RequireOwnerAsync(profileId, db, http, ct);
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        return await BlockingEventStream.SubscribeAsync<ClubNotification>(receiver, $"club_notif_{profileId}", scopeFactory, viewerId,
            (queued, fresh, token) => fresh.ClubNotifications.Include(n => n.Club).Include(n => n.Profile)
                .FirstOrDefaultAsync(n => n.Id == queued.Id && n.ProfileId == profileId, token), ct);
    }

    [Subscribe(With = nameof(SubscribeToLiveChatMessageAdded))]
    [Topic("live_{liveId}_chat")]
    [Authorize]
    public LiveChatMessage LiveChatMessageAdded(Guid liveId, [EventMessage] LiveChatMessage message) => message;

    public async ValueTask<ISourceStream<LiveChatMessage>> SubscribeToLiveChatMessageAdded(
        Guid liveId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        var moderator = BlockingEventStream.IsErpModerator(http);
        db.UseBlockVisibility(viewerId);
        if (!await CanViewLiveAsync(db, liveId, viewerId, moderator, ct))
            throw new GraphQLException("No tienes acceso al chat de este live.");
        return await BlockingEventStream.SubscribeAsync<LiveChatMessage>(receiver, $"live_{liveId}_chat", scopeFactory, viewerId,
            async (queued, fresh, token) => await CanViewLiveAsync(fresh, liveId, viewerId, moderator, token)
                ? await fresh.LiveChatMessages.Include(m => m.SenderProfile)
                    .Include(m => m.LiveSession).ThenInclude(l => l!.Club)
                    .FirstOrDefaultAsync(m => m.Id == queued.Id && m.LiveSessionId == liveId, token)
                : null, ct);
    }

    /// <summary>Incluye aprobación o expulsión propia; no exige membresía activa para recibirla.</summary>
    [Subscribe(With = nameof(SubscribeToClubMembershipChanged))]
    [Topic("club_member_{profileId}")]
    [Authorize]
    public ClubMember ClubMembershipChanged(Guid profileId, [EventMessage] ClubMember member) => member;

    public async ValueTask<ISourceStream<ClubMember>> SubscribeToClubMembershipChanged(
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        [Service] IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        await RequireOwnerAsync(profileId, db, http, ct);
        var viewerId = await CommunityBlockPolicy.ViewerProfileIdAsync(db, http, ct);
        return await BlockingEventStream.SubscribeAsync<ClubMember>(receiver, $"club_member_{profileId}", scopeFactory, viewerId,
            (queued, fresh, token) => queued.ProfileId == profileId
                ? fresh.ClubMembers.Include(m => m.Club).Include(m => m.Profile)
                    .FirstOrDefaultAsync(m => m.ClubId == queued.ClubId && m.ProfileId == profileId, token)
                : Task.FromResult<ClubMember?>(null), ct);
    }

    private static async Task RequireOwnerAsync(
        Guid profileId, CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        var actor = await CommunityBlockPolicy.RequireActorAsync(db, http, ct);
        if (actor.Id != profileId)
            throw new GraphQLException("Solo puedes suscribirte a tus propias notificaciones y membresías.");
    }

    private static async Task<bool> CanViewLiveAsync(
        CommunityDbContext db, Guid liveId, Guid? viewerId, bool moderator, CancellationToken ct)
    {
        var clubId = await db.LiveSessions.Where(l => l.Id == liveId)
            .Select(l => (Guid?)l.ClubId).FirstOrDefaultAsync(ct);
        return clubId is { } id
            && await BlockingEventStream.CanViewClubAsync(db, id, viewerId, moderator, true, ct);
    }
}
