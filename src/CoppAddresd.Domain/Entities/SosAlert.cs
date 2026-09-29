using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Alerta SOS del botón de pánico del paciente (change sos-panic-real,
/// schema <c>app</c>). La identidad del paciente SIEMPRE se deriva del JWT;
/// la creación es atómica y protegida por dos índices únicos:
/// <list type="bullet">
/// <item><c>(patient_id, idempotency_key)</c>: semántica completa de
/// <c>Idempotency-Key</c> (reintento idéntico → la misma fila; payload
/// distinto → rechazo).</item>
/// <item>Índice único parcial sobre <c>patient_id</c> donde
/// <c>status = 'Activa'</c>: garantía de una sola alerta activa por
/// paciente, incluso bajo carrera.</item>
/// </list>
/// Privacidad (REQ-SOS-06): las coordenadas y el teléfono de destino viven
/// SOLO aquí; está prohibido registrarlos en logs de aplicación (los logs
/// usan únicamente alertId/patientId/correlationId y estados de canal).
/// </summary>
public sealed class SosAlert
{
    public Guid Id { get; set; }

    /// <summary>Paciente dueño de la alerta (<c>app.patient_profiles.id</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>Clave de idempotencia (UUIDv4 del encabezado <c>Idempotency-Key</c>).</summary>
    public string IdempotencyKey { get; set; } = default!;

    /// <summary>
    /// SHA-256 de las coordenadas y datos del payload original: permite
    /// distinguir "misma clave + payload idéntico" (reintento → 200) de
    /// "misma clave + payload distinto" (colisión → 409) sin almacenar el
    /// cuerpo completo. No contiene PII.
    /// </summary>
    public string PayloadHash { get; set; } = default!;

    public SosAlertStatus Status { get; set; } = SosAlertStatus.Activa;

    // --- Ubicación GPS (opcional, best-effort) ---

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Precisión declarada por el dispositivo en metros.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>Instante de la captura GPS reportado por la app (UTC).</summary>
    public DateTime? LocationCapturedAt { get; set; }

    // --- Canal SMS (principal: contacto de emergencia) ---

    /// <summary>
    /// Snapshot del teléfono de destino normalizado a E.164 server-side,
    /// tomado de <c>patient_profiles.emergency_contact.phone</c> en el
    /// momento de la activación (el contacto posterior del perfil no muta
    /// alertas ya creadas). Nunca se registra en logs.
    /// </summary>
    public string DestinationPhoneE164 { get; set; } = default!;

    public SosChannelStatus SmsChannelStatus { get; set; } = SosChannelStatus.Pendiente;

    /// <summary>Última actualización del canal SMS (UTC).</summary>
    public DateTime? SmsUpdatedAt { get; set; }

    /// <summary>
    /// Código corto del resultado del canal SIN PII (ej. <c>twilio:21614</c>,
    /// <c>network</c>, <c>timeout</c>). Nunca contiene teléfono ni cuerpo.
    /// </summary>
    public string? SmsDetail { get; set; }

    // --- Canal push (staff asignado) ---

    public SosChannelStatus PushChannelStatus { get; set; } = SosChannelStatus.Pendiente;

    public DateTime? PushUpdatedAt { get; set; }

    /// <summary>Número de tokens push alcanzados (métrica operacional, sin PII).</summary>
    public int? PushRecipients { get; set; }

    public string? PushDetail { get; set; }

    // --- Transiciones terminales (actor + marca de tiempo) ---

    /// <summary>Usuario (auth.users) del staff ERP que atendió la alerta.</summary>
    public Guid? AttendedBy { get; set; }

    public DateTime? AttendedAt { get; set; }

    /// <summary>Usuario (auth.users) dueño del paciente que la canceló.</summary>
    public Guid? CancelledBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation

    public PatientProfile? Patient { get; set; }
}
