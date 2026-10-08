using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Cuerpo opcional para compatibilidad con clientes que no usan la cookie
/// HttpOnly. La fuente principal del refresh token es la cookie
/// <c>copp_refresh_token</c>.
/// </summary>
public record RefreshTokenRequest
{
    [RegularExpression("^[a-zA-Z0-9-]{1,32}$")]
    public string Application { get; init; } = "erp";

    public string? RefreshToken { get; init; }
}
