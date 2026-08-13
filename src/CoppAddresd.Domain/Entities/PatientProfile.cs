namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Perfil de paciente en la aplicación móvil. Se relaciona 1:1 con
/// <c>auth.users</c> (sin FK en el modelo EF: el usuario vive en el esquema
/// auth, fuera de este DbContext; la restricción se crea por SQL en la
/// migración). Los datos de identidad básica (nombres, email) residen en
/// auth.users para no duplicarlos.
/// </summary>
public sealed class PatientProfile
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en <c>auth.users</c>.</summary>
    public Guid UserId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    /// <summary>Género declarado por el paciente (M, F, O...).</summary>
    public string? Gender { get; set; }

    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}