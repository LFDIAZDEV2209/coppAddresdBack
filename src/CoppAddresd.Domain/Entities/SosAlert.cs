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

    // --- Signos vitales (opcional, demo) ---

    /// <summary>Frecuencia cardíaca en lpm reportada al activar (demo por ahora).</summary>
    public int? HeartRate { get; set; }

    /// <summary>Saturación de oxígeno en porcentaje reportada al activar (demo por ahora).</summary>
    public int? Spo2 { get; set; }

    /// <summary>Presión arterial reportada al activar, formato "160/110" (demo por ahora).</summary>
    public string? BloodPressure { get; set; }

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

    /// <summary>
    /// Estado de entrega reportado por Twilio vía webhook (queued/sent/
    /// delivered/undelivered/failed). Null = sin callback aún.
    /// </summary>
    public string? SmsDeliveryStatus { get; set; }

    // --- Canal voz (llamada TTS al contacto de emergencia) ---

    /// <summary>
    /// Estado de la llamada de voz al mismo teléfono de destino del SMS
    /// (contacto de emergencia). Mismo ciclo de vida que los demás canales.
    /// </summary>
    public SosChannelStatus VoiceChannelStatus { get; set; } = SosChannelStatus.Pendiente;

    /// <summary>Última actualización del canal de voz (UTC).</summary>
    public DateTime? VoiceUpdatedAt { get; set; }

    /// <summary>
    /// Código corto del resultado del canal SIN PII (ej. <c>twilio:21210</c>,
    /// <c>network</c>, <c>timeout</c>). Nunca contiene teléfono ni guion TTS.
    /// </summary>
    public string? VoiceDetail { get; set; }

    /// <summary>Sid de la llamada Twilio (CA...) para soporte/trazabilidad.</summary>
    public string? VoiceProviderCallId { get; set; }

    /// <summary>
    /// Estado de la llamada reportado por Twilio vía webhook
    /// (queued/initiated/ringing/in-progress/completed/busy/no-answer/failed/canceled).
    /// </summary>
    public string? VoiceCallStatus { get; set; }

    /// <summary>
    /// Quién contestó según Twilio (human/machine_start/...), solo si se
    /// habilitó detección de contestador. Null = desconocido.
    /// </summary>
    public string? VoiceAnsweredBy { get; set; }

    /// <summary>Duración de la llamada en segundos (reportada al completar).</summary>
    public int? VoiceDurationSeconds { get; set; }

    // --- Canal correo (contacto de emergencia) ---

    /// <summary>
    /// Snapshot del correo del contacto de emergencia tomado de
    /// <c>patient_profiles.emergency_contact.email</c> al momento de la
    /// activación (el perfil posterior no muta alertas creadas). Null = el
    /// contacto no tiene correo → el canal queda <c>SinDestino</c> y no se
    /// envía nada. Nunca se registra en logs.
    /// </summary>
    public string? DestinationEmail { get; set; }

    /// <summary>
    /// Estado del correo al contacto de emergencia (<c>SinDestino</c> cuando
    /// no hay correo registrado). Mismo ciclo de vida que los demás canales.
    /// </summary>
    public SosChannelStatus EmailChannelStatus { get; set; } = SosChannelStatus.Pendiente;

    /// <summary>Última actualización del canal de correo (UTC).</summary>
    public DateTime? EmailUpdatedAt { get; set; }

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
