using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Vinculación de un alta a una cuenta existente (mismo correo): suma los
/// roles/permisos seleccionados en modo unión pura — nada se elimina y las
/// credenciales no cambian.
/// </summary>
public record LinkUserAccountRequest
{
    [Required(ErrorMessage = "Email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; init; } = string.Empty;

    /// <summary>Roles a sumar a la cuenta existente (opcional).</summary>
    public Guid[]? RoleIds { get; init; }

    /// <summary>Permisos directos a sumar a la cuenta existente (opcional).</summary>
    public Guid[]? PermissionIds { get; init; }
}

/// <summary>
/// Estado del correo para el preflight del alta de usuarios: si ya tiene
/// cuenta, si está activa y si tiene contraseña (cuenta real vs. invitación
/// pendiente). El nombre permite mostrar la tarjeta de vinculación.
/// </summary>
public record UserEmailAvailabilityResponse(
    bool Exists,
    bool IsActive,
    bool HasPassword,
    Guid? UserId,
    string? FirstName,
    string? LastName);
