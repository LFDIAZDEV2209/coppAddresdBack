using System.ComponentModel.DataAnnotations.Schema;

namespace CoppAddresd.Community.Entities;

/// <summary>Grupo de chat de la comunidad (mensajería grupal entre amigos).</summary>
public sealed class ChatGroup
{
    public Guid Id { get; set; }

    /// <summary>Nombre del grupo (3-100 caracteres).</summary>
    public string Name { get; set; } = default!;

    /// <summary>Perfil creador del grupo (no puede ser removido ni abandonar el grupo).</summary>
    public Guid CreatedByProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatGroupMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];

    /// <summary>Cantidad de miembros (se rellena en memoria en la consulta Groups).</summary>
    [NotMapped]
    public int MemberCount { get; set; }

    /// <summary>Último mensaje del grupo (se rellena en memoria en la consulta Groups).</summary>
    [NotMapped]
    public Message? LastMessage { get; set; }
}
