using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Valida las referencias a catálogos de los payloads de paciente (FKs del
/// perfil + colecciones hijas) en una sola pasada contra la BD. Lanza
/// <see cref="InvalidOperationException"/> con el detalle si alguna
/// referencia no existe o la geografía es inconsistente.
/// </summary>
internal static class CatalogGuard
{
    public static async Task ValidateAsync(
        ICatalogRepository catalogs,
        Guid? documentTypeId,
        Guid? ethnicityId,
        Guid? bloodTypeId,
        Guid? countryId,
        Guid? stateId,
        Guid? cityId,
        Guid? insurerId,
        IReadOnlyList<DiagnosisInput>? diagnoses,
        IReadOnlyList<MedicationInput>? medications,
        IReadOnlyList<AllergyInput>? allergies,
        CancellationToken ct)
    {
        var insurerIds = insurerId is null ? [] : new[] { insurerId.Value };
        var icd10Ids = (diagnoses ?? []).Select(d => d.Icd10CodeId).ToHashSet();
        var medicationIds = (medications ?? []).Select(m => m.MedicationId).ToHashSet();
        var allergenIds = (allergies ?? []).Select(a => a.AllergenId).ToHashSet();

        var result = await catalogs.ValidateAsync(
            documentTypeId,
            ethnicityId,
            bloodTypeId,
            countryId,
            stateId,
            cityId,
            insurerIds,
            icd10Ids,
            medicationIds,
            allergenIds,
            ct);

        var errors = new List<string>();

        if (!result.DocumentTypeExists)
            errors.Add("El tipo de documento no existe en el catálogo.");
        if (!result.EthnicityExists)
            errors.Add("La etnia no existe en el catálogo.");
        if (!result.BloodTypeExists)
            errors.Add("El grupo sanguíneo no existe en el catálogo.");
        if (!result.CountryExists)
            errors.Add("El país no existe en el catálogo.");
        if (!result.StateExists)
            errors.Add("El estado no existe en el catálogo.");
        if (!result.StateInCountry)
            errors.Add("El estado no pertenece al país seleccionado.");
        if (!result.CityExists)
            errors.Add("La ciudad no existe en el catálogo.");
        if (!result.CityInState)
            errors.Add("La ciudad no pertenece al estado seleccionado.");

        if (insurerId is not null && !result.ExistingInsurerIds.Contains(insurerId.Value))
            errors.Add($"La aseguradora {insurerId.Value} no existe en el catálogo.");

        var missingIcd10 = icd10Ids.Except(result.ExistingIcd10CodeIds).ToList();
        if (missingIcd10.Count > 0)
            errors.Add($"Los códigos ICD-10 {string.Join(", ", missingIcd10)} no existen en el catálogo.");

        var missingMedications = medicationIds.Except(result.ExistingMedicationIds).ToList();
        if (missingMedications.Count > 0)
            errors.Add($"Los medicamentos {string.Join(", ", missingMedications)} no existen en el catálogo.");

        var missingAllergens = allergenIds.Except(result.ExistingAllergenIds).ToList();
        if (missingAllergens.Count > 0)
            errors.Add($"Los alergenos {string.Join(", ", missingAllergens)} no existen en el catálogo.");

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }
}