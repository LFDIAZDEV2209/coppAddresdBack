using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IAuthService
{
    Task<TokenResult?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<TokenResult?> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<Guid?> GetUserIdByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> LogoutAsync(Guid userId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Define la primera contrasena de una cuenta OTP (sin password previo).</summary>
    Task<(bool Success, string? Error)> SetFirstPasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}