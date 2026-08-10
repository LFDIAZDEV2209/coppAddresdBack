using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<RefreshTokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> LogoutAsync(Guid userId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}
