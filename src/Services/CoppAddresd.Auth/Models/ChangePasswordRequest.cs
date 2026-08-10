using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record ChangePasswordRequest
{
    [Required(ErrorMessage = "CurrentPassword es requerido")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "NewPassword es requerido")]
    [MinLength(8, ErrorMessage = "NewPassword debe tener al menos 8 caracteres")]
    public string NewPassword { get; init; } = string.Empty;
}
