namespace CoppAddresd.Telemedicine.Domain.Enums;

/// <summary>
/// Ciclo de vida de una solicitud de telemedicina creada por un paciente.
/// <c>Converted</c> indica que la solicitud derivó en una cita confirmada.
/// </summary>
public enum AppointmentRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Converted,
}

/// <summary>
/// Ciclo de vida de una cita de telemedicina (agregado raíz del módulo).
/// La reprogramación NO es un estado persistente: se refleja en
/// <see cref="CoppAddresd.Telemedicine.Domain.Entities.AppointmentReschedule"/>
/// (historial) y la cita vuelve a <c>Confirmed</c> con nueva hora.
/// </summary>
public enum AppointmentStatus
{
    Requested,
    Confirmed,
    InProgress,
    Completed,
    Cancelled,
    NoShow,
}

/// <summary>Quién ejecutó una cancelación (para trazabilidad).</summary>
public enum CancelledBy
{
    Patient,
    Professional,
    Admin,
    System,
}

/// <summary>Quién solicitó/ejecutó una reprogramación.</summary>
public enum RescheduleRequestedBy
{
    Patient,
    Professional,
    Admin,
    System,
}

/// <summary>
/// Ciclo de vida de la sala virtual en el proveedor. Independiente del estado
/// de la cita: una sala puede fallar sin cambiar el estado de la cita.
/// </summary>
public enum VirtualRoomStatus
{
    Created,
    Waiting,
    Active,
    Ended,
    Expired,
    Failed,
}

/// <summary>
/// Ciclo de vida de la sesión de video. Separada del estado de la cita y de la
/// sala: la sesión es el registro de una conexión concreta.
/// </summary>
public enum TelemedicineSessionStatus
{
    Created,
    Waiting,
    Active,
    Ended,
    Expired,
    Failed,
}

/// <summary>Estado del encuentro clínico asociado a la consulta.</summary>
public enum EncounterStatus
{
    Draft,
    Completed,
    Cancelled,
}

/// <summary>
/// Tipos de alerta de la bandeja. Son eventos de dominio materializados; el
/// canal de entrega (email/push/SMS) es responsabilidad futura y desacoplada.
/// </summary>
public enum AlertType
{
    NewRequest,
    RequestApproved,
    RequestRejected,
    NewAppointment,
    UpcomingAppointment,
    AppointmentRescheduled,
    AppointmentCancelled,
    PatientWaiting,
    PatientJoined,
    ParticipantLeft,
    SessionEnded,
    NoShow,
    System,
}

/// <summary>
/// Tipos de notificación despachada con deduplicación por cita (F2). El valor
/// forma parte de la clave única <c>(appointment_id, kind)</c> de
/// <c>tele.notification_dispatch</c>: cada tipo se envía como máximo una vez
/// por cita. El destinatario y los canales los decide el emisor.
/// </summary>
public enum NotificationDispatchKind
{
    /// <summary>Primer recordatorio: push al paciente 24 h antes (configurable).</summary>
    Reminder24h,

    /// <summary>Segundo recordatorio: push (+ SMS según ventana) al paciente 1 h antes.</summary>
    Reminder1h,

    /// <summary>Recordatorio al profesional 1 h antes (push).</summary>
    ProfessionalReminder1h,
}

/// <summary>Severidad de una alerta para priorizar la bandeja.</summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
}

/// <summary>Destinatario de una alerta (usuario concreto o ámbito).</summary>
public enum AlertRecipientType
{
    User,
    Professional,
    ClinicAdmin,
    System,
}
