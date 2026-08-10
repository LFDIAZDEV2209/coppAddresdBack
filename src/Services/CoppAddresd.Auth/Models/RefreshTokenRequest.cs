using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record RefreshTokenRequest
{
    [Required(ErrorMessage = "RefreshToken es requerido")]
    public string RefreshToken { get; init; } = string.Empty;
}
