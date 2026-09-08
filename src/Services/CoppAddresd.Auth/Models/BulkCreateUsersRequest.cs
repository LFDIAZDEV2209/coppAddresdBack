using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Solicitud de creación masiva de usuarios.
/// Cada fila se procesa de forma independiente; una falla no detiene las demás.
/// Máximo 500 filas por solicitud.
/// </summary>
public record BulkCreateUsersRequest
{
    /// <summary>Lista de usuarios a crear. Máximo 500 filas.</summary>
    [Required(ErrorMessage = "Rows es requerido")]
    public List<BulkCreateUserRow> Rows { get; init; } = [];
}

/// <summary>
/// Fila individual para creación masiva de usuario.
/// </summary>
public record BulkCreateUserRow
{
    /// <summary>Nombre del usuario.</summary>
    [Required(ErrorMessage = "FirstName es requerido")]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Apellido del usuario.</summary>
    [Required(ErrorMessage = "LastName es requerido")]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    /// <summary>Email del usuario (debe ser único).</summary>
    [Required(ErrorMessage = "Email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Nombre del rol a asignar (opcional, case-insensitive).
    /// Si se proporciona, se resuelve al rol activo correspondiente.
    /// </summary>
    public string? RoleName { get; init; }

    /// <summary>
    /// Estado del usuario: "activo" (default) o "inactivo" (case-insensitive).
    /// </summary>
    public string? Status { get; init; }
}
