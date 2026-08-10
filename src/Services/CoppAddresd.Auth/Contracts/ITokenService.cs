using CoppAddresd.Auth.Entities;

namespace CoppAddresd.Auth.Contracts;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default);
}
