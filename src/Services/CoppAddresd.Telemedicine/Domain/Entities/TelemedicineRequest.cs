using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Solicitud de telemedicina creada por un paciente. Representa la intención de
/// agenda; su confirmación deriva en una <see cref="Appointment"/>.
/// Los vínculos a paciente/profesional/especialidad/sede son referencias
/// débiles (Id) al ERP: este microservicio no posee esos datos maestros.
/// </summary>
public sealed class TelemedicineRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Paciente que solicita (referencia débil a <c>app.patient_profiles</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>Profesional preferido (opcional: el paciente puede elegir solo especialidad).</summary>
    public Guid? ProfessionalId { get; set; }

    /// <summary>Especialidad o servicio solicitado (referencia débil a <c>erp.specialties</c>).</summary>
    public Guid SpecialtyId { get; set; }

    /// <summary>Organización del contexto (referencia débil a <c>erp.organizations</c>).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Clínica del contexto (referencia débil a <c>erp.clinics</c>).</summary>
    public Guid? ClinicId { get; set; }

    /// <summary>Sede donde se espera la atención (referencia débil a <c>erp.locations</c>).</summary>
    public Guid? LocationId { get; set; }

    /// <summary>Fecha/hora preferida por el paciente (opcional).</summary>
    public DateTimeOffset? PreferredStart { get; set; }

    /// <summary>Motivo de la consulta.</summary>
    public string? Reason { get; set; }

    public AppointmentRequestStatus Status { get; set; } = AppointmentRequestStatus.Pending;

    /// <summary>Observaciones internas (recepción/administración).</summary>
    public string? Notes { get; set; }

    /// <summary>Usuario autenticado que creó la solicitud (<c>auth.users</c>).</summary>
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = [];
}
