using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del módulo de pacientes. Define persistencia del agregado
/// <see cref="PatientProfile"/> y del catálogo de aseguradoras; la
/// implementación EF vive en Infrastructure.
/// </summary>
public interface IPatientRepository
{
    /// <summary>Paciente completo con agregado (insurer + diagnósticos + medicamentos + alergias + vitales). Excluye eliminados (soft delete).</summary>
    Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PatientProfile?> GetByMedicalRecordNumberAsync(string mrn, CancellationToken ct = default);

    /// <summary>Paciente por usuario de Auth (contexto del JWT). Excluye eliminados (soft delete).</summary>
    Task<PatientProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Lista paginada del directorio con filtros y orden estable (CreatedAt desc, Id desc). Solo pacientes no eliminados; <paramref name="clinicId"/> filtra por clínica (frontera de datos Fase 4) y <paramref name="professionalId"/> restringe al alcance "propio" del profesional (solo pacientes asignados activos).</summary>
    Task<(IReadOnlyList<PatientProfile> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? insurerId,
        Guid? clinicId,
        Guid? professionalId,
        CancellationToken ct = default);

    Task<PatientProfile> AddAsync(PatientProfile patient, CancellationToken ct = default);

    Task UpdateAsync(PatientProfile patient, CancellationToken ct = default);

    /// <summary>Soft delete: marca <c>deleted_at</c> (trazabilidad PHI); las filas hijas se conservan.</summary>
    Task SoftDeleteAsync(PatientProfile patient, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Insurer>> ListInsurersAsync(CancellationToken ct = default);

    Task<Insurer?> GetInsurerByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>¿El paciente tiene una asignación activa con el profesional? (alcance de datos "propios").</summary>
    Task<bool> IsAssignedToProfessionalAsync(Guid patientId, Guid professionalId, CancellationToken ct = default);

    /// <summary>Asigna un profesional a un paciente (idempotente: reactiva la asignación existente).</summary>
    Task AssignProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        Guid? clinicId,
        string relationshipType,
        Guid? createdBy,
        CancellationToken ct = default);

    /// <summary>Desasigna un profesional de un paciente (soft: marca Inactive).</summary>
    Task RemoveProfessionalAsync(Guid patientId, Guid professionalId, CancellationToken ct = default);

    /// <summary>Asignaciones de un paciente con datos del profesional (nombre y tipo), activas e inactivas.</summary>
    Task<IReadOnlyList<PatientProfessionalAssignmentView>> ListAssignmentsAsync(Guid patientId, CancellationToken ct = default);
}

/// <summary>Vista de una asignación paciente ↔ profesional (para la UI del detalle).</summary>
public sealed record PatientProfessionalAssignmentView(
    Guid ProfessionalId,
    string FullName,
    string? ProfessionalTypeName,
    string RelationshipType,
    string Status,
    DateTime CreatedAt);