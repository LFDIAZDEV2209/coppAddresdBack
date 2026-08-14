namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Código ICD-10 del catálogo de referencia del módulo de pacientes. La
/// descripción es propiedad del código, no de cada diagnóstico: los diagnósticos
/// (<see cref="PatientDiagnosis"/>) solo referencian el código.
/// </summary>
public sealed class Icd10Code
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PatientDiagnosis> Diagnoses { get; set; } = [];
}