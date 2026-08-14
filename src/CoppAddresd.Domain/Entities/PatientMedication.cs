namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Medicamento prescrito a un paciente, referenciando el catálogo
/// <see cref="Entities.Medication"/>. NDC/RxNorm/clase farmacológica son del
/// catálogo; la frecuencia y el orden son específicos del paciente.
/// </summary>
public sealed class PatientMedication
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Medicamento del catálogo de referencia (FK a <see cref="Entities.Medication"/>).</summary>
    public Guid MedicationId { get; set; }

    public string? Frequency { get; set; }

    /// <summary>Posición dentro de la lista de medicamentos del paciente.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PatientProfile? Patient { get; set; }

    public Medication? Medication { get; set; }
}