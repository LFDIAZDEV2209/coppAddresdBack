using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Cita de telemedicina: agregado raíz del agendamiento. Cada cita vincula a un
/// paciente con un profesional en una sede/contexto para un rango de tiempo.
/// La integridad del calendario (no doble reserva) se protege con un índice
/// único parcial y verificación transaccional.
/// </summary>
public sealed class TelemedicineAppointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Solicitud que dio origen a la cita (opcional: admite alta directa).</summary>
    public Guid? RequestId { get; set; }

    /// <summary>Paciente atendido (referencia débil a <c>app.patient_profiles</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>Profesional asignado (referencia débil a <c>erp.professionals</c>).</summary>
    public Guid ProfessionalId { get; set; }

    /// <summary>Especialidad de la consulta (referencia débil a <c>erp.specialties</c>).</summary>
    public Guid SpecialtyId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? ClinicId { get; set; }

    /// <summary>Sede/contexto de la atención (referencia débil a <c>erp.locations</c>).</summary>
    public Guid? LocationId { get; set; }

    /// <summary>Inicio programado (zonas horarias: el cliente envía su offset).</summary>
    public DateTimeOffset ScheduledStart { get; set; }

    public DateTimeOffset ScheduledEnd { get; set; }

    public int DurationMinutes { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Requested;

    /// <summary>Número de reprogramaciones aplicadas (limite por configuración).</summary>
    public int RescheduleCount { get; set; }

    /// <summary>Razón de la cancelación actual (estado vigente; historial en <c>appointment_cancellations</c>).</summary>
    public string? CancellationReason { get; set; }

    public CancelledBy? CancelledBy { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? NoShowReason { get; set; }

    /// <summary>Token de concurrencia optimista (xmin de PostgreSQL).</summary>
    public uint Version { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public TelemedicineRequest? Request { get; set; }

    public ICollection<AppointmentCancellation> Cancellations { get; set; } = [];

    public ICollection<AppointmentReschedule> Reschedules { get; set; } = [];

    public VirtualRoom? Room { get; set; }

    public ICollection<TelemedicineSession> Sessions { get; set; } = [];

    public ClinicalEncounter? Encounter { get; set; }
}
