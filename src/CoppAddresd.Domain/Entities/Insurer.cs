namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Aseguradora (EPS / proveedor de salud). Catálogo de referencia del módulo
/// de pacientes; cada paciente se afilia a una vía <see cref="PatientProfile.InsurerId"/>.
/// </summary>
public sealed class Insurer
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PatientProfile> Patients { get; set; } = [];
}