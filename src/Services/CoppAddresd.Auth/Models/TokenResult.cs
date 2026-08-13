namespace CoppAddresd.Auth.Models;

/// <summary>
/// Resultado interno del servicio de auth con ambos tokens. El refresh token
/// nunca viaja en el body de la respuesta HTTP: el controller lo coloca en
/// cookie HttpOnly.
/// </summary>
public record TokenResult(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn);