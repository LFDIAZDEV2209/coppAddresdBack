using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record CreateUserRequest
{
    [Required(ErrorMessage = "Email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password es requerido")]
    [MinLength(8, ErrorMessage = "Password debe tener al menos 8 caracteres")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "FirstName es requerido")]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required(ErrorMessage = "LastName es requerido")]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;
}
