namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Configuración operativa de telemedicina por organización/clínica. Todas las
/// reglas de negocio del agendamiento y de la sala son parámetros, no constantes
/// hardcodeadas: cambiar la lógica no requiere desplegar código.
/// </summary>
public sealed class TelemedicineSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    /// <summary>Null = configuración global de la organización.</summary>
    public Guid? ClinicId { get; set; }

    public int DefaultAppointmentDurationMinutes { get; set; } = 30;

    /// <summary>Anticipación mínima para solicitar/agendar (horas).</summary>
    public int MinAdvanceBookingHours { get; set; } = 2;

    /// <summary>Ventana máxima de reserva a futuro (días).</summary>
    public int MaxAdvanceBookingDays { get; set; } = 30;

    /// <summary>Límite de reprogramaciones por cita.</summary>
    public int MaxReschedules { get; set; } = 2;

    /// <summary>Minutos antes del inicio en que la sala acepta participantes.</summary>
    public int RoomOpenBeforeMinutes { get; set; } = 10;

    /// <summary>Minutos después del fin de la cita en que la sala deja de aceptar participantes.</summary>
    public int RoomCloseAfterMinutes { get; set; } = 15;

    /// <summary>TTL del token de acceso a la sala (segundos).</summary>
    public int AccessTokenTtlSeconds { get; set; } = 900;

    public int MaxParticipants { get; set; } = 2;

    /// <summary>
    /// Interruptor maestro de notificaciones push/SMS (F2). En <c>false</c> ni el
    /// barrido de recordatorios ni los hooks de eventos envían notificaciones
    /// para esta organización/clínica.
    /// </summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Horas de anticipación del primer recordatorio de cita (push al paciente).
    /// 0 o negativo = deshabilitado. Default 24.
    /// </summary>
    public int ReminderFirstHoursBefore { get; set; } = 24;

    /// <summary>
    /// Horas de anticipación del segundo recordatorio (push al paciente y push
    /// al profesional). 0 o negativo = deshabilitado. Default 1.
    /// </summary>
    public int ReminderSecondHoursBefore { get; set; } = 1;

    /// <summary>
    /// Horas de anticipación con las que el recordatorio del paciente incluye
    /// SMS (además del push). Default 1 (coincide con el segundo recordatorio).
    /// </summary>
    public int SmsReminderHoursBefore { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
