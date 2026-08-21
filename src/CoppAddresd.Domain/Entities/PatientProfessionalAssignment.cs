namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Asignación de un paciente a un profesional clínico (relación del dominio:
/// "el profesional X atiende al paciente Y"). Es la base del alcance de datos
/// "propios" (Patients.ViewOwn): un profesional solo consulta y gestiona los
/// pacientes con asignación activa. Un paciente puede tener varios
/// profesionales (médico + nutricionista + psicólogo).
/// </summary>
public sealed class PatientProfessionalAssignment
{
    public Guid PatientId { get; set; }

    /// <summary>Id del profesional en <c>erp.professionals</c> (extensión clínica del empleado).</summary>
    public Guid ProfessionalId { get; set; }

    /// <summary>Clínica del contexto en que se realizó la asignación (frontera de datos Fase 4).</summary>
    public Guid? ClinicId { get; set; }

    /// <summary>Tipo de relación: "Assigned" (asignado), "Primary" (responsable principal)... Catálogo extensible.</summary>
    public string RelationshipType { get; set; } = "Assigned";

    /// <summary>Estado de la asignación: Active / Inactive.</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Usuario de <c>auth.users</c> que creó la asignación (auditoría).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PatientProfile? Patient { get; set; }

    public Professional? Professional { get; set; }

    public Clinic? Clinic { get; set; }
}