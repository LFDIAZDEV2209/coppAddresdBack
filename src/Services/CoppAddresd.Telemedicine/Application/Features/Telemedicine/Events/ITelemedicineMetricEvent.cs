using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;

/// <summary>
/// Contrato marcador para eventos de métricas de telemedicina procesados en background con 0ms de bloqueo.
/// </summary>
public interface ITelemedicineMetricEvent;

/// <summary>Evento emitido al agendar una nueva cita.</summary>
public sealed record AppointmentScheduledMetricEvent(
    Guid AppointmentId,
    Guid ProfessionalId,
    Guid? ClinicId,
    DateOnly ScheduledDate,
    int ScheduledHour,
    AppointmentStatus Status
) : ITelemedicineMetricEvent;

/// <summary>Evento emitido cuando cambia el estado de una cita (Completada, Cancelada, NoShow).</summary>
public sealed record AppointmentStatusChangedMetricEvent(
    Guid AppointmentId,
    Guid ProfessionalId,
    Guid? ClinicId,
    DateOnly ScheduledDate,
    AppointmentStatus OldStatus,
    AppointmentStatus NewStatus
) : ITelemedicineMetricEvent;

/// <summary>
/// Evento emitido cuando la sala de una cita se abre por primera vez (la rama
/// perezosa sin sala de join-token/session-start). Una reapertura no lo emite.
/// </summary>
public sealed record RoomOpenedMetricEvent(
    Guid AppointmentId,
    Guid ProfessionalId,
    Guid? ClinicId,
    DateOnly ScheduledDate
) : ITelemedicineMetricEvent;

/// <summary>Evento emitido cuando se inicia una sesión de video (session/start).</summary>
public sealed record SessionStartedMetricEvent(
    Guid AppointmentId,
    Guid ProfessionalId,
    Guid? ClinicId,
    DateOnly ScheduledDate
) : ITelemedicineMetricEvent;

/// <summary>
/// Evento emitido cuando termina una sesión de video (fin manual, webhook
/// <c>room-ended</c> o barrido). <paramref name="DurationSeconds"/> puede ser
/// null (sesión sin duración calculable): el contador de sesiones igual suma 1.
/// </summary>
public sealed record SessionEndedMetricEvent(
    Guid AppointmentId,
    Guid ProfessionalId,
    Guid? ClinicId,
    DateOnly ScheduledDate,
    long? DurationSeconds
) : ITelemedicineMetricEvent;

/// <summary>
/// Evento emitido cuando se persiste un mensaje del chat de la consulta. El rol
/// sale del JWT en el servidor (Professional | Patient | Supervisor), nunca del
/// cuerpo del mensaje.
/// </summary>
public sealed record ChatMessageSentMetricEvent(
    Guid AppointmentId,
    Guid ProfessionalId,
    Guid? ClinicId,
    DateOnly ScheduledDate,
    string Role
) : ITelemedicineMetricEvent;
