using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Alerta de la bandeja del profesional/administrador. Es la materialización de
/// un evento de dominio; el envío por canales externos (email/push/SMS) es
/// responsabilidad futura y desacoplada de este agregado.
/// </summary>
public sealed class TelemedicineAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Destinatario concreto (usuario de <c>auth.users</c>). Null para destinatarios por ámbito.</summary>
    public Guid? RecipientUserId { get; set; }

    public AlertRecipientType RecipientType { get; set; } = AlertRecipientType.User;

    /// <summary>Id de ámbito cuando la alerta es por clínica/rol (ej. ClinicAdmin → clínica).</summary>
    public Guid? RecipientScopeId { get; set; }

    public AlertType Type { get; set; }

    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;

    public string Title { get; set; } = default!;

    public string? Body { get; set; }

    /// <summary>Cita relacionada (para acciones contextuales de la bandeja).</summary>
    public Guid? RelatedAppointmentId { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
