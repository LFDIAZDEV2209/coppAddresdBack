using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.GraphQL.Types;

/// <summary>Alcance del feed de publicaciones.</summary>
public enum FeedScope
{
    ForYou,
    Following,
}

/// <summary>Alcance de envío de mensajes masivos desde el sistema.</summary>
public enum MessageScope
{
    /// <summary>Perfiles activos con más de 7 días sin actividad (inactivos).</summary>
    Inactive,

    /// <summary>Todos los perfiles activos (excluye perfiles del sistema).</summary>
    AllActive,
}

/// <summary>Un perfil de la comunidad enriquecido con la relación respecto al usuario actual.</summary>
public sealed class Person
{
    public Profile Profile { get; init; } = null!;
    public bool IsFollowing { get; init; }
    public bool IsFollower { get; init; }
    public bool IsFriend { get; init; }
}

/// <summary>Última conversación con un par (peer) de la mensajería privada.</summary>
public sealed class Conversation
{
    public Profile Peer { get; init; } = null!;
    public Message? LastMessage { get; init; }
}
