namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Grupo sanguíneo del catálogo clínico (ABO + Rh). Código estable (ej. "O+")
/// usado como valor canónico en el perfil del paciente.
/// </summary>
public sealed class BloodType
{
    public Guid Id { get; set; }

    /// <summary>Código canónico (A+, A-, B+, B-, AB+, AB-, O+, O-).</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}