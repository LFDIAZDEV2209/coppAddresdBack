namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Perfil de paciente. Originalmente concebido como perfil 1:1 con
/// <c>auth.users</c> (app móvil); hoy también aloja el directorio clínico del
/// ERP, por lo que <see cref="UserId"/> es opcional (los pacientes de prueba
/// no tienen usuario). Los datos de identidad básica (nombres, email) viven
/// aquí cuando no existe cuenta de usuario.
/// </summary>
public sealed class PatientProfile
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en <c>auth.users</c>. Null para pacientes sin cuenta.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Número de historia clínica (MRN). Identificador externo único del paciente.</summary>
    public string? MedicalRecordNumber { get; set; }

    public string FirstName { get; set; } = default!;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = default!;

    /// <summary>Tipo de documento del catálogo administrativo (FK, orientado a USA).</summary>
    public Guid? DocumentTypeId { get; set; }

    public string? DocumentNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    /// <summary>Género declarado (Femenino, Masculino...).</summary>
    public string? Gender { get; set; }

    /// <summary>Etnia del catálogo demográfico (FK, categorías OMB).</summary>
    public Guid? EthnicityId { get; set; }

    /// <summary>Grupo sanguíneo del catálogo clínico (FK).</summary>
    public Guid? BloodTypeId { get; set; }

    /// <summary>Código telefónico E.164 sin '+' (ej. "1" para USA).</summary>
    public string? PhoneCountryCode { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    /// <summary>Ciudad del catálogo geográfico (FK).</summary>
    public Guid? CityId { get; set; }

    /// <summary>Estado del catálogo geográfico (FK, ej. CA).</summary>
    public Guid? StateId { get; set; }

    /// <summary>País del catálogo geográfico (FK).</summary>
    public Guid? CountryId { get; set; }

    public string? PostalCode { get; set; }

    /// <summary>Relación del contacto de emergencia (Familiar, Amigo...).</summary>
    public string? EmergencyContact { get; set; }

    public Guid? InsurerId { get; set; }

    /// <summary>Id de afiliado dentro de la aseguradora.</summary>
    public string? MemberId { get; set; }

    /// <summary>Estado civil (Soltero/a, Casado/a...).</summary>
    public string? MaritalStatus { get; set; }

    public string? SmokingStatus { get; set; }

    public string? AlcoholStatus { get; set; }

    public string? ExerciseLevel { get; set; }

    public string? Disability { get; set; }

    public string? HospitalizationHistory { get; set; }

    public string? SurgeryHistory { get; set; }

    /// <summary>Estado operativo del directorio (Activo, Inactivo, Pendiente).</summary>
    public string Status { get; set; } = "Activo";

    public string? Notes { get; set; }

    /// <summary>Clínica propietaria del paciente (frontera de datos Fase 4). Null para el directorio legacy importado sin clínica.</summary>
    public Guid? ClinicId { get; set; }

    /// <summary>Sede donde se atiende habitualmente el paciente. Null si no aplica.</summary>
    public Guid? LocationId { get; set; }

    /// <summary>Usuario de <c>auth.users</c> que creó el registro (auditoría).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Último usuario de <c>auth.users</c> que modificó el registro (auditoría).</summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Soft delete: cuando no es null el paciente se considera eliminado y se
    /// excluye de listados y consultas (trazabilidad PHI, nunca hard delete).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Insurer? Insurer { get; set; }

    public Clinic? Clinic { get; set; }

    public Location? Location { get; set; }

    public DocumentType? DocumentType { get; set; }

    public Ethnicity? Ethnicity { get; set; }

    public BloodType? BloodType { get; set; }

    public Country? Country { get; set; }

    public State? State { get; set; }

    public City? City { get; set; }

    public ICollection<PatientDiagnosis> Diagnoses { get; set; } = [];

    public ICollection<PatientMedication> Medications { get; set; } = [];

    public ICollection<PatientAllergy> Allergies { get; set; } = [];

    public ICollection<VitalSign> VitalSigns { get; set; } = [];

/// <summary>Profesionales asignados al paciente (alcance de datos "propios").</summary>
    public ICollection<PatientProfessionalAssignment> Assignments { get; set; } = [];

    public ICollection<NutritionPlan> NutritionPlans { get; set; } = [];

    public ICollection<RoutineAssignment> RoutineAssignments { get; set; } = [];
}