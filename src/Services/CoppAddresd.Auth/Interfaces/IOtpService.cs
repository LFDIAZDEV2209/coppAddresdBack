using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

/// <summary>
/// Flujo de primer inicio de sesión por número de identificación:
/// consulta de contactos asociados, envío y verificación de OTP, y
/// aprovisionamiento de la cuenta del paciente al verificar.
/// </summary>
public interface IOtpService
{
    Task<IdLookupResponse?> LookupByIdAsync(IdLookupRequest request, CancellationToken ct = default);

    Task<(bool Success, string? Error, SendOtpResponse? Result)> SendOtpAsync(
        SendOtpRequest request,
        CancellationToken ct = default);

    Task<TokenResult?> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);
}
