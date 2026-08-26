namespace CoppAddresd.Community.Entities;

/// <summary>Miembro de un grupo de chat (relación perfil ↔ grupo).</summary>
public sealed class ChatGroupMember
{
    public Guid GroupId { get; set; }

    public Guid ProfileId { get; set; }

    /// <summary>Fecha en la que el perfil se unió al grupo.</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public ChatGroup? Group { get; set; }
    public Profile? Profile { get; set; }
}
