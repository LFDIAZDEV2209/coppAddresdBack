namespace CoppAddresd.Community.Entities;

/// <summary>Notificación in-app de un club (aprobaciones, invitaciones, eventos, lives).</summary>
public sealed class ClubNotification
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    /// <summary>Perfil destinatario de la notificación.</summary>
    public Guid ProfileId { get; set; }

    public ClubNotificationType Type { get; set; }

    /// <summary>Payload opcional (JSON con contexto, p. ej. id de evento/live).</summary>
    public string? Payload { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Club? Club { get; set; }
    public Profile? Profile { get; set; }
}