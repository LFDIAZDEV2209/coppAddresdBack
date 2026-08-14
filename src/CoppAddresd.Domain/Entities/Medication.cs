namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Medicamento del catálogo de referencia del módulo de pacientes. NDC, RxNorm
/// y clase farmacológica son propiedades del fármaco, no de cada prescripción:
/// las prescripciones (<see cref="PatientMedication"/>) solo referencian el
/// medicamento y conservan la frecuencia y el orden propios de cada paciente.
/// </summary>
public sealed class Medication
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Ndc { get; set; }

    public string? RxNorm { get; set; }

    public string? DrugClass { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PatientMedication> PatientMedications { get; set; } = [];
}