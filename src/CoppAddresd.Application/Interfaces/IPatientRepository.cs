using CoppAddresd.Application.Features.Patients;
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

    /// <summary>Lista paginada del directorio con filtros y orden estable (CreatedAt desc, Id desc). Solo pacientes no eliminados; <paramref name="clinicId"/> filtra por clínica (frontera de datos Fase 4), <paramref name="professionalId"/> restringe al alcance "propio" del profesional (solo pacientes asignados activos) y <paramref name="stateCode"/> filtra por estado de EE. UU. (selección del mapa). <paramref name="sortBy"/> viene de la whitelist de campos y <paramref name="sortDir"/> es asc/desc; null → CreatedAt desc.</summary>
    Task<(IReadOnlyList<PatientProfile> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? insurerId,
        Guid? clinicId,
        Guid? professionalId,
        string? sortBy,
        string? sortDir,
        string? stateCode = null,
        CancellationToken ct = default
    );

    Task<PatientProfile> AddAsync(PatientProfile patient, CancellationToken ct = default);

    Task UpdateAsync(PatientProfile patient, CancellationToken ct = default);

    /// <summary>Soft delete: marca <c>deleted_at</c> (trazabilidad PHI); las filas hijas se conservan.</summary>
    Task SoftDeleteAsync(PatientProfile patient, CancellationToken ct = default);

    /// <summary>
    /// Snapshot mínimo (estado + clínica) para el toggle Activo↔Inactivo sin
    /// cargar el agregado completo; null si el paciente no existe (excluye
    /// eliminados).
    /// </summary>
    Task<PatientStatusSnapshot?> GetStatusSnapshotAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Actualiza únicamente <c>status</c> (+ auditoría) sin tocar campos ni
    /// colecciones hijas. Devuelve false si el paciente no existe.
    /// </summary>
    Task<bool> UpdateStatusAsync(
        Guid id,
        string status,
        Guid? updatedBy,
        CancellationToken ct = default
    );

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Insurer>> ListInsurersAsync(CancellationToken ct = default);

    Task<Insurer?> GetInsurerByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>¿El paciente tiene una asignación activa con el profesional? (alcance de datos "propios").</summary>
    Task<bool> IsAssignedToProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Estadísticas del directorio con el mismo alcance que <see cref="ListAsync"/>
    /// (clínica activa + alcance propio): total, activos, nuevos desde
    /// <paramref name="monthStartUtc"/> y sin asignación activa. Un solo
    /// roundtrip agregado (sin counts independientes).
    /// </summary>
    Task<PatientStatsDto> GetStatsAsync(
        Guid? clinicId,
        Guid? professionalId,
        DateTime monthStartUtc,
        CancellationToken ct = default
    );

    /// <summary>Asigna un profesional a un paciente (idempotente: reactiva la asignación existente).</summary>
    Task AssignProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        Guid? clinicId,
        string relationshipType,
        Guid? createdBy,
        CancellationToken ct = default
    );

    /// <summary>Desasigna un profesional de un paciente (soft: marca Inactive).</summary>
    Task RemoveProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        CancellationToken ct = default
    );

    /// <summary>Asignaciones de un paciente con datos del profesional (nombre y tipo), activas e inactivas.</summary>
    Task<IReadOnlyList<PatientProfessionalAssignmentView>> ListAssignmentsAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Nombres de profesionales clínicos por id (índice id → nombre completo),
    /// para poblar el listado del directorio en una sola consulta agrupada
    /// contra el núcleo HR (erp.employees). Sin ids → diccionario vacío.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetProfessionalNamesAsync(
        IReadOnlyCollection<Guid> professionalIds,
        CancellationToken ct = default
    );

    /// <summary>
    /// Documentos que ya existen en la BD (case-insensitive), útiles para
    /// bulk create: una sola query con WHERE LOWER(document_number) = ANY(batch).
    /// Devuelve los documentos encontrados (lowercase).
    /// </summary>
    Task<IReadOnlyList<string>> GetExistingDocumentNumbersAsync(
        IReadOnlyCollection<string> documentNumbers,
        CancellationToken ct = default
    );
}

/// <summary>Vista de una asignación paciente ↔ profesional (para la UI del detalle).</summary>
public sealed record PatientProfessionalAssignmentView(
    Guid ProfessionalId,
    string FullName,
    string? ProfessionalTypeName,
    string RelationshipType,
    string Status,
    DateTime CreatedAt
);

/// <summary>Snapshot mínimo del paciente para el toggle de estado.</summary>
public sealed record PatientStatusSnapshot(string Status, Guid? ClinicId);
