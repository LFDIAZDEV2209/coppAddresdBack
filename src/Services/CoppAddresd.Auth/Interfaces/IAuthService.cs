using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IAuthService
{
    Task<TokenResult?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<TokenResult?> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<Guid?> GetUserIdByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Código de la aplicación (erp, app) con la que se emitió un refresh token
    /// activo. Null si el token no existe, expiró o fue revocado. No rota ni
    /// modifica el token: sirve para validar la aplicación antes de refrescar.
    /// </summary>
    Task<string?> GetRefreshTokenApplicationCodeAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> LogoutAsync(Guid userId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Define la primera contrasena de una cuenta OTP (sin password previo).</summary>
    Task<(bool Success, string? Error)> SetFirstPasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>
    /// Solicita la eliminación de la cuenta: suspende el acceso a la aplicación
    /// indicada, revoca sus sesiones y programa la purga tras la retención. Si
    /// el usuario no conserva acceso a otra aplicación, además se desactiva.
    /// </summary>
    Task<(bool Success, string? Error, DateTime? PurgeAfter)> RequestAccountDeletionAsync(
        Guid userId, string application, CancellationToken ct = default);

    /// <summary>
    /// Emite un código opaco de un solo uso (vida corta) para abrir la web de
    /// eliminación de cuenta. Null si el usuario no tiene acceso activo a la aplicación.
    /// </summary>
    Task<AccountDeletionHandoffResponse?> CreateAccountDeletionHandoffAsync(
        Guid userId, string application, CancellationToken ct = default);
}