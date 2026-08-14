using CoppAddresd.Auth.Entities;

namespace CoppAddresd.Auth.Interfaces;

public interface ITokenService
{
    /// <summary>Genera el access token con <paramref name="audience"/> como claim `aud` (código de aplicación).</summary>
    /// <param name="permissions">Códigos de permiso actuales del usuario (directos + via rol); cada uno se emite como claim <c>permission</c>.</param>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles, string audience, IEnumerable<string> permissions);
    Task<string> GenerateRefreshTokenAsync(Guid userId, Guid? applicationId, CancellationToken ct = default);
}
