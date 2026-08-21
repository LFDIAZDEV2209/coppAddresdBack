using CoppAddresd.Community.Entities;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

namespace CoppAddresd.Community.GraphQL.Subscriptions;

/// <summary>Suscripciones de la comunidad (mensajería en tiempo real).</summary>
[Authorize]
public sealed class CommunitySubscription
{
    [Subscribe]
    [Topic("message_{conversationKey}")]
    public Message MessageAdded(string conversationKey, [EventMessage] Message message) => message;
}
