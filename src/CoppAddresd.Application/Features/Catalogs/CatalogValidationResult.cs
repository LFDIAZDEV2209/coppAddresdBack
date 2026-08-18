namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>
/// Resultado de la validación de referencias a catálogos. Cada flag indica si
/// el id enviado existe (cuando el id era nulo el flag queda en <c>true</c>);
/// los sets contienen los ids existentes de cada catálogo hijo.
/// </summary>
public sealed record CatalogValidationResult(
    bool DocumentTypeExists,
    bool EthnicityExists,
    bool BloodTypeExists,
    bool CountryExists,
    bool StateExists,
    bool StateInCountry,
    bool CityExists,
    bool CityInState,
    IReadOnlySet<Guid> ExistingInsurerIds,
    IReadOnlySet<Guid> ExistingIcd10CodeIds,
    IReadOnlySet<Guid> ExistingMedicationIds,
    IReadOnlySet<Guid> ExistingAllergenIds)
{
    /// <summary>
    /// Verdadero si todas las referencias existen y la geografía es
    /// consistente (estado dentro del país y ciudad dentro del estado).
    /// </summary>
    public bool IsValid =>
        DocumentTypeExists &&
        EthnicityExists &&
        BloodTypeExists &&
        CountryExists &&
        StateExists &&
        StateInCountry &&
        CityExists &&
        CityInState;
}