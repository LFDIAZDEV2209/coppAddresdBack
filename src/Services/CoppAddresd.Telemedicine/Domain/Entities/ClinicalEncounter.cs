using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Encuentro clínico asociado a la consulta. Separa la información clínica de
/// la operativa de la cita/sesión. <c>ClinicalData</c> es un <c>jsonb</c>
/// extensible (motivo, evaluación, plan, indicaciones...) para no inventar un
/// EHR completo: el ERP podrá ampliar el esquema sin romper el modelo.
/// </summary>
public sealed class ClinicalEncounter
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid PatientId { get; set; }

    public Guid ProfessionalId { get; set; }

    public DateTimeOffset EncounterDate { get; set; } = DateTimeOffset.UtcNow;

    public EncounterStatus Status { get; set; } = EncounterStatus.Draft;

    /// <summary>Datos clínicos estructurados extensibles (jsonb).</summary>
    public string? ClinicalData { get; set; }

    public string? Notes { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public TelemedicineAppointment? Appointment { get; set; }

    public TelemedicineSession? Session { get; set; }
}
