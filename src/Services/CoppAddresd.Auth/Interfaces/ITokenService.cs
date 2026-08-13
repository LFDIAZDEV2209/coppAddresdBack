using CoppAddresd.Auth.Entities;

namespace CoppAddresd.Auth.Interfaces;

public interface ITokenService
{
    /// <summary>Genera el access token con <paramref name="audience"/> como claim `aud` (código de aplicación).</summary>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles, string audience);
    Task<string> GenerateRefreshTokenAsync(Guid userId, Guid? applicationId, CancellationToken ct = default);
}
