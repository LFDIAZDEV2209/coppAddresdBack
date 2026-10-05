using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Cuerpo opcional de refresh y logout. La fuente principal del refresh token
/// es la cookie HttpOnly <c>copp_refresh_token_{application}</c>; el cuerpo
/// existe por compatibilidad con clientes que no usan la cookie.
/// </summary>
public record RefreshTokenRequest
{
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Código de la aplicación que pide el refresh ("erp", "app"). Selecciona
    /// la cookie propia de esa aplicación y permite rechazar tokens emitidos
    /// para otra. Opcional: los clientes anteriores a la cookie por aplicación
    /// no lo envían y siguen usando la cookie <c>copp_refresh_token</c>.
    /// </summary>
    public string? Application { get; init; }
}
