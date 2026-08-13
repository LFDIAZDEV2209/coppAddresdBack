using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Cuerpo opcional para compatibilidad con clientes que no usan la cookie
/// HttpOnly. La fuente principal del refresh token es la cookie
/// <c>copp_refresh_token</c>.
/// </summary>
public record RefreshTokenRequest
{
    public string? RefreshToken { get; init; }
}
