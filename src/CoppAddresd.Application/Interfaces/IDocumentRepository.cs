using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio de documentos clínicos (Fase 5). Solo persistencia; la
/// composición de queries vive en la capa Application.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>Documento por id con navegaciones (paciente, tipo, categoría).</summary>
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Documento raíz (ParentDocumentId = null) por id, con tipo y categoría.</summary>
    Task<Document?> GetRootAsync(Guid rootId, CancellationToken ct = default);

    /// <summary>
    /// Lista paginada de la ÚLTIMA versión de cada documento (fila raíz por
    /// documento, con conteo de versiones). Excluye eliminados.
    /// </summary>
    Task<(IReadOnlyList<Document> Items, int Total)> ListLatestAsync(
        Guid? patientId,
        Guid? clinicId,
        Guid? categoryId,
        Guid? documentTypeId,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Todas las versiones de un documento raíz (ordenadas por versión desc).</summary>
    Task<IReadOnlyList<Document>> ListVersionsAsync(Guid rootId, CancellationToken ct = default);

    /// <summary>Próximo número de versión para el documento raíz (máximo existente + 1).</summary>
    Task<int> GetNextVersionAsync(Guid rootId, CancellationToken ct = default);

    /// <summary>Clínica del paciente (null si no existe o no tiene clínica).</summary>
    Task<Guid?> GetPatientClinicIdAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>¿Existe el paciente en el directorio (sin contar eliminados)?</summary>
    Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct = default);

    Task<ClinicalDocumentType?> GetTypeByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentCategory>> ListCategoriesAsync(bool activeOnly = true, CancellationToken ct = default);

    Task<IReadOnlyList<ClinicalDocumentType>> ListTypesAsync(Guid? categoryId = null, bool activeOnly = true, CancellationToken ct = default);

    Task AddAsync(Document document, CancellationToken ct = default);

    Task UpdateAsync(Document document, CancellationToken ct = default);

    /// <summary>Eliminación lógica del documento completo (raíz + versiones), con actor.</summary>
    Task<bool> SoftDeleteFamilyAsync(Guid rootId, Guid? deletedBy, CancellationToken ct = default);
}