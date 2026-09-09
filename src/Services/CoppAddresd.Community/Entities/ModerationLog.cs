namespace CoppAddresd.Community.Entities;

/// <summary>Registro de acciones de moderación dentro de un club (auditoría).</summary>
public sealed class ModerationLog
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    /// <summary>Perfil que ejecuta la acción (admin/moderador).</summary>
    public Guid ActorProfileId { get; set; }

    /// <summary>Perfil afectado (aprobado, expulsado, silenciado, reportado…).</summary>
    public Guid TargetProfileId { get; set; }

    /// <summary>Acción ejecutada (p. ej. AprobarSolicitud, Expulsar, Silenciar…).</summary>
    public string Action { get; set; } = default!;

    /// <summary>Motivo opcional de la acción.</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Club? Club { get; set; }
    public Profile? ActorProfile { get; set; }
    public Profile? TargetProfile { get; set; }
}