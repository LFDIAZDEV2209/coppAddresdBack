using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record LoginRequest
{
    [Required(ErrorMessage = "Email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password es requerido")]
    public string Password { get; init; } = string.Empty;
}
