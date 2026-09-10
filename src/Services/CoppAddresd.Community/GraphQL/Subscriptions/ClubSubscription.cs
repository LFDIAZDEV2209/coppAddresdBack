using CoppAddresd.Community.Entities;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using HotChocolate.Types;

namespace CoppAddresd.Community.GraphQL.Subscriptions;

/// <summary>
/// Suscripciones del módulo de clubes (in-memory, patrón de CommunitySubscription):
/// posts nuevos del club, notificaciones in-app, chat de live y cambios de membresía.
/// </summary>
[ExtendObjectType(typeof(CommunitySubscription))]
public sealed class ClubSubscription
{
    /// <summary>Publicación nueva publicada en un club.</summary>
    [Subscribe(With = nameof(SubscribeToClubPostAdded))]
    [Topic("club_{clubId}_posts")]
    [Authorize]
    public Post ClubPostAdded(Guid clubId, [EventMessage] Post post)
        => post;

    public async ValueTask<ISourceStream<Post>> SubscribeToClubPostAdded(
        Guid clubId,
        [Service] ITopicEventReceiver receiver,
        CancellationToken ct)
        => await receiver.SubscribeAsync<Post>($"club_{clubId}_posts", ct);

    /// <summary>Notificación in-app nueva para el perfil autenticado.</summary>
    [Subscribe(With = nameof(SubscribeToClubNotificationAdded))]
    [Topic("club_notif_{profileId}")]
    [Authorize]
    public ClubNotification ClubNotificationAdded(Guid profileId, [EventMessage] ClubNotification notification)
        => notification;

    public async ValueTask<ISourceStream<ClubNotification>> SubscribeToClubNotificationAdded(
        Guid profileId,
        [Service] ITopicEventReceiver receiver,
        CancellationToken ct)
        => await receiver.SubscribeAsync<ClubNotification>($"club_notif_{profileId}", ct);

    /// <summary>Mensaje nuevo en el chat de un live.</summary>
    [Subscribe(With = nameof(SubscribeToLiveChatMessageAdded))]
    [Topic("live_{liveId}_chat")]
    [Authorize]
    public LiveChatMessage LiveChatMessageAdded(Guid liveId, [EventMessage] LiveChatMessage message)
        => message;

    public async ValueTask<ISourceStream<LiveChatMessage>> SubscribeToLiveChatMessageAdded(
        Guid liveId,
        [Service] ITopicEventReceiver receiver,
        CancellationToken ct)
        => await receiver.SubscribeAsync<LiveChatMessage>($"live_{liveId}_chat", ct);

    /// <summary>Cambio de membresía del perfil autenticado (aprobación, expulsión, unión).</summary>
    [Subscribe(With = nameof(SubscribeToClubMembershipChanged))]
    [Topic("club_member_{profileId}")]
    [Authorize]
    public ClubMember ClubMembershipChanged(Guid profileId, [EventMessage] ClubMember member)
        => member;

    public async ValueTask<ISourceStream<ClubMember>> SubscribeToClubMembershipChanged(
        Guid profileId,
        [Service] ITopicEventReceiver receiver,
        CancellationToken ct)
        => await receiver.SubscribeAsync<ClubMember>($"club_member_{profileId}", ct);
}