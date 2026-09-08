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
