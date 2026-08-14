using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del módulo de pacientes. Define persistencia del agregado
/// <see cref="PatientProfile"/> y del catálogo de aseguradoras; la
/// implementación EF vive en Infrastructure.
/// </summary>
public interface IPatientRepository
{
    /// <summary>Paciente completo con agregado (insurer + diagnósticos + medicamentos + alergias + vitales).</summary>
    Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PatientProfile?> GetByMedicalRecordNumberAsync(string mrn, CancellationToken ct = default);

    /// <summary>Lista paginada del directorio con filtros y orden estable (CreatedAt desc, Id desc).</summary>
    Task<(IReadOnlyList<PatientProfile> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? insurerId,
        CancellationToken ct = default);

    Task<PatientProfile> AddAsync(PatientProfile patient, CancellationToken ct = default);

    Task UpdateAsync(PatientProfile patient, CancellationToken ct = default);

    Task DeleteAsync(PatientProfile patient, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Insurer>> ListInsurersAsync(CancellationToken ct = default);

    Task<Insurer?> GetInsurerByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene (o crea, race-safe vía unique constraint) un alergeno del catálogo
    /// <c>app.allergens</c>. El nombre se normaliza con trim.
    /// </summary>
    Task<Allergen> GetOrCreateAllergenAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Obtiene (o crea, race-safe vía unique constraint) un código ICD-10 del
    /// catálogo <c>app.icd10_codes</c>. La descripción solo se usa al crear la
    /// fila del catálogo; las filas existentes no se sobrescriben.
    /// </summary>
    Task<Icd10Code> GetOrCreateIcd10CodeAsync(string code, string? description, CancellationToken ct = default);

    /// <summary>
    /// Obtiene (o crea, race-safe vía unique constraint) un medicamento del
    /// catálogo <c>app.medications</c>. NDC/RxNorm/clase solo se usan al crear
    /// la fila del catálogo; las filas existentes no se sobrescriben.
    /// </summary>
    Task<Medication> GetOrCreateMedicationAsync(
        string name, string? ndc, string? rxNorm, string? drugClass, CancellationToken ct = default);
}