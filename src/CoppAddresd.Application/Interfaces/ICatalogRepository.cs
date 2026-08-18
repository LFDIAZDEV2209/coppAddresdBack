using CoppAddresd.Application.Features.Catalogs;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio de los catálogos del módulo de pacientes (geográficos,
/// administrativos y clínicos). Solo lectura; el seed vive en las migraciones.
/// </summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<Country>> ListCountriesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<State>> ListStatesByCountryAsync(Guid countryId, CancellationToken ct = default);

    /// <summary>
    /// Busca ciudades de un estado por nombre (ILIKE, usa índice trigram).
    /// Devuelve hasta <paramref name="limit"/> filas ordenadas por nombre.
    /// </summary>
    Task<IReadOnlyList<City>> SearchCitiesAsync(
        Guid stateId, string? search, int limit = 30, CancellationToken ct = default);

    Task<IReadOnlyList<PostalCode>> ListPostalCodesByCityAsync(Guid cityId, CancellationToken ct = default);

    Task<IReadOnlyList<BloodType>> ListBloodTypesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DocumentType>> ListDocumentTypesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Ethnicity>> ListEthnicitiesAsync(CancellationToken ct = default);

    /// <summary>Busca códigos ICD-10 por código o descripción (índice trigram).</summary>
    Task<IReadOnlyList<Icd10Code>> SearchIcd10CodesAsync(
        string? search, int limit = 30, CancellationToken ct = default);

    /// <summary>Busca medicamentos por nombre o NDC (índice trigram).</summary>
    Task<IReadOnlyList<Medication>> SearchMedicationsAsync(
        string? search, int limit = 30, CancellationToken ct = default);

    /// <summary>Busca alergenos por nombre (índice trigram).</summary>
    Task<IReadOnlyList<Allergen>> SearchAllergensAsync(
        string? search, int limit = 30, CancellationToken ct = default);

    /// <summary>
    /// Valida en una sola pasada los identificadores de catálogo enviados por
    /// el cliente (FKs del paciente + referencias de las colecciones hijas).
    /// Cuando el id es <c>null</c> la validación se considera correcta.
    /// </summary>
    Task<CatalogValidationResult> ValidateAsync(
        Guid? documentTypeId,
        Guid? ethnicityId,
        Guid? bloodTypeId,
        Guid? countryId,
        Guid? stateId,
        Guid? cityId,
        IReadOnlyCollection<Guid> insurerIds,
        IReadOnlyCollection<Guid> icd10CodeIds,
        IReadOnlyCollection<Guid> medicationIds,
        IReadOnlyCollection<Guid> allergenIds,
        CancellationToken ct = default);
}