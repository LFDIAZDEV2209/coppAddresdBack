namespace CoppAddresd.Community.Entities;

public sealed class Message
{
    public Guid Id { get; set; }
    public Guid SenderProfileId { get; set; }

    /// <summary>Destinatario en mensajería 1:1. Nulo para mensajes de grupo (ver ConversationId).</summary>
    public Guid? RecipientProfileId { get; set; }

    /// <summary>Grupo al que pertenece el mensaje (mensajería grupal). Nulo en 1:1.</summary>
    public Guid? ConversationId { get; set; }

    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>Perfil del admin que desencadenó el envío (mensajes enviados desde el perfil del sistema).</summary>
    public Guid? TriggeredByProfileId { get; set; }
}
