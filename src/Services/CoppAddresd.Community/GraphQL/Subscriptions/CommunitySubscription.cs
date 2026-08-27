using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Subscriptions;

/// <summary>Suscripciones de la comunidad (mensajería en tiempo real).</summary>
[Authorize]
public sealed class CommunitySubscription
{
    [Subscribe(With = nameof(SubscribeToMessageAdded))]
    [Topic("message_{conversationKey}")]
    public Message MessageAdded(string conversationKey, [EventMessage] Message message) => message;

    /// <summary>
    /// Notifica en tiempo real cuando alguien publica un post. Los posts son contenido
    /// público, así que basta con el [Authorize] de la clase (sin chequeo de participante).
    /// </summary>
    [Subscribe]
    [Topic("post_added")]
    public Post PostAdded([EventMessage] Post post) => post;

    /// <summary>
    /// Mensajes en tiempo real de un grupo de chat. Solo los miembros pueden suscribirse.
    /// </summary>
    [Subscribe(With = nameof(SubscribeToGroupMessageAdded))]
    [Topic("group_{groupId}")]
    public Message GroupMessageAdded(Guid groupId, [EventMessage] Message message) => message;

    /// <summary>
    /// Eventos del feed en vivo de la comunidad (publicaciones, rachas, hitos, logros).
    /// </summary>
    [Subscribe]
    [Topic("feed_event_added")]
    public FeedEvent FeedEventAdded([EventMessage] FeedEvent feedEvent) => feedEvent;

    /// <summary>Comentarios en tiempo real (incluye respuestas anidadas).</summary>
    [Subscribe]
    [Topic("comment_added")]
    public Comment CommentAdded([EventMessage] Comment comment) => comment;

    /// <summary>
    /// Cambios de un grupo (creación, renombrado, altas/bajas de miembros). Solo miembros.
    /// </summary>
    [Subscribe(With = nameof(SubscribeToGroupChanged))]
    [Topic("group_{groupId}_changed")]
    public ChatGroup GroupChanged(Guid groupId, [EventMessage] ChatGroup group) => group;

    /// <summary>Valida la membresía al grupo antes de suscribirse a sus mensajes.</summary>
    public async ValueTask<ISourceStream<Message>> SubscribeToGroupMessageAdded(
        Guid groupId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        CancellationToken ct)
    {
        await CommunityQuery.RequireGroupMembershipAsync(db, http, groupId, ct);
        return await receiver.SubscribeAsync<Message>($"group_{groupId}", ct);
    }

    /// <summary>Valida la membresía al grupo antes de suscribirse a sus cambios.</summary>
    public async ValueTask<ISourceStream<ChatGroup>> SubscribeToGroupChanged(
        Guid groupId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        CancellationToken ct)
    {
        await CommunityQuery.RequireGroupMembershipAsync(db, http, groupId, ct);
        return await receiver.SubscribeAsync<ChatGroup>($"group_{groupId}_changed", ct);
    }

    /// <summary>
    /// Valida que el usuario autenticado sea participante de la conversación antes de
    /// suscribirse. La clave del tópico se deriva de dos IDs públicos de perfil, por lo
    /// que sin este chequeo cualquier usuario autenticado podría espiar conversaciones ajenas.
    /// </summary>
    public async ValueTask<ISourceStream<Message>> SubscribeToMessageAdded(
        string conversationKey,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventReceiver receiver,
        CancellationToken ct)
    {
        await EnsureParticipantAsync(conversationKey, db, http, ct);
        return await receiver.SubscribeAsync<Message>($"message_{conversationKey}", ct);
    }

    /// <summary>Comprueba que el usuario actual forma parte de la conversación (sender o recipient).</summary>
    internal static async Task<Guid> EnsureParticipantAsync(
        string conversationKey,
        CommunityDbContext db,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CommunityQuery.CurrentUserId(http);
        if (userId is null)
            throw new GraphQLException("No estás autenticado.");
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");
        if (!TryParseConversationKey(conversationKey, out var a, out var b) || (profile.Id != a && profile.Id != b))
            throw new GraphQLException("No puedes suscribirte a esta conversación.");
        return profile.Id;
    }

    /// <summary>Interpreta una clave de conversación "idA:idB" (ordinal) como dos GUIDs.</summary>
    internal static bool TryParseConversationKey(string key, out Guid a, out Guid b)
    {
        a = default;
        b = default;
        var parts = key.Split(':');
        if (parts.Length != 2) return false;
        return Guid.TryParse(parts[0], out a) && Guid.TryParse(parts[1], out b);
    }
}
