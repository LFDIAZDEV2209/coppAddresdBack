using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Registro de entrega de una notificación a un paciente derivada de una alerta
/// (SPEC A13). Es el log auditable que alimenta la vista "Notificaciones
/// enviadas" y los gráficos del ERP. Un envío masivo genera una fila por
/// paciente y canal; un envío de prueba del Template Studio también se registra.
/// </summary>
public sealed class HealthTestNotification
{
    public Guid Id { get; set; }

    /// <summary>Alerta origen (null en envíos de prueba del Template Studio).</summary>
    public Guid? AlertId { get; set; }

    /// <summary>Paciente destinatario (null solo en pruebas sin paciente).</summary>
    public Guid? PatientId { get; set; }

    public NotificationChannel Channel { get; set; }

    /// <summary>Plantilla usada (null si el cuerpo se editó libremente).</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Destino real: teléfono E.164 (sms) o id de usuario (community).</summary>
    public string Recipient { get; set; } = default!;

    /// <summary>Cuerpo final ya renderizado (sin placeholders).</summary>
    public string RenderedBody { get; set; } = default!;

    public NotificationStatus Status { get; set; } = NotificationStatus.queued;

    /// <summary>Proveedor usado (ej. <c>community</c>, <c>noop</c>, <c>twilio</c>).</summary>
    public string Provider { get; set; } = default!;

    public string? ProviderMessageId { get; set; }

    public string? Error { get; set; }

    /// <summary>Usuario (auth.users) que disparó el envío (null = sistema).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    // Navigation
    public HealthTestAlert? Alert { get; set; }

    public PatientProfile? Patient { get; set; }

    public HealthTestNotificationTemplate? Template { get; set; }
}
