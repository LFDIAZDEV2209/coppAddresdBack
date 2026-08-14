namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Diagnóstico clínico de un paciente, codificado con ICD-10 del catálogo
/// <see cref="Entities.Icd10Code"/>. Un paciente tiene uno o varios diagnósticos;
/// uno puede marcarse como principal.
/// </summary>
public sealed class PatientDiagnosis
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Código ICD-10 del catálogo de referencia (FK). La descripción vive en el catálogo.</summary>
    public Guid Icd10CodeId { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PatientProfile? Patient { get; set; }

    public Icd10Code? Icd10Code { get; set; }
}