using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>Diagnóstico resuelto contra el catálogo ICD-10 (id del código).</summary>
public sealed record ResolvedDiagnosis(Guid Icd10CodeId, bool IsPrimary);

/// <summary>Medicamento resuelto contra el catálogo (id del fármaco + frecuencia por paciente).</summary>
public sealed record ResolvedMedication(Guid MedicationId, string? Frequency);

/// <summary>Alergia resuelta contra el catálogo de alergenos (id + notas por paciente).</summary>
public sealed record ResolvedAllergy(Guid AllergenId, string? Notes);

/// <summary>
/// Resuelve los valores libres de un payload (códigos ICD-10, medicamentos y
/// alergenos) contra los catálogos de referencia, creándolos si no existen
/// (get-or-create). La caché por instancia evita consultas repetidas al
/// catálogo dentro de un mismo request; una instancia = una petición.
/// </summary>
public sealed class PatientCatalogResolver(IPatientRepository repository)
{
    private readonly Dictionary<string, Guid> _icd10Codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _medications = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _allergens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resuelve los diagnósticos; descarta entradas sin código.</summary>
    public async Task<IReadOnlyList<ResolvedDiagnosis>> ResolveDiagnosesAsync(
        IReadOnlyList<DiagnosisInput>? inputs, CancellationToken ct)
    {
        var result = new List<ResolvedDiagnosis>();
        foreach (var input in inputs ?? [])
        {
            if (string.IsNullOrWhiteSpace(input.Icd10Code))
                continue;

            var code = input.Icd10Code.Trim();
            if (!_icd10Codes.TryGetValue(code, out var id))
            {
                var catalog = await repository.GetOrCreateIcd10CodeAsync(code, input.Description, ct);
                _icd10Codes[code] = id = catalog.Id;
            }

            result.Add(new ResolvedDiagnosis(id, input.IsPrimary));
        }

        return result;
    }

    /// <summary>Resuelve los medicamentos; descarta entradas sin nombre.</summary>
    public async Task<IReadOnlyList<ResolvedMedication>> ResolveMedicationsAsync(
        IReadOnlyList<MedicationInput>? inputs, CancellationToken ct)
    {
        var result = new List<ResolvedMedication>();
        foreach (var input in inputs ?? [])
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                continue;

            var name = input.Name.Trim();
            if (!_medications.TryGetValue(name, out var id))
            {
                var catalog = await repository.GetOrCreateMedicationAsync(name, input.Ndc, input.RxNorm, input.DrugClass, ct);
                _medications[name] = id = catalog.Id;
            }

            result.Add(new ResolvedMedication(
                id,
                string.IsNullOrWhiteSpace(input.Frequency) ? null : input.Frequency.Trim()));
        }

        return result;
    }

    /// <summary>Resuelve las alergias; descarta entradas sin alergeno.</summary>
    public async Task<IReadOnlyList<ResolvedAllergy>> ResolveAllergiesAsync(
        IReadOnlyList<AllergyInput>? inputs, CancellationToken ct)
    {
        var result = new List<ResolvedAllergy>();
        foreach (var input in inputs ?? [])
        {
            if (string.IsNullOrWhiteSpace(input.Allergen))
                continue;

            var allergen = input.Allergen.Trim();
            if (!_allergens.TryGetValue(allergen, out var id))
            {
                var catalog = await repository.GetOrCreateAllergenAsync(allergen, ct);
                _allergens[allergen] = id = catalog.Id;
            }

            result.Add(new ResolvedAllergy(
                id,
                string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()));
        }

        return result;
    }
}