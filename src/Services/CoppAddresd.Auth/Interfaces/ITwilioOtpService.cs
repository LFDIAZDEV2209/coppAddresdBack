namespace CoppAddresd.Auth.Interfaces;

/// <summary>
/// Resultado del envío de una <c>Verification</c> a Twilio Verify V2.
/// Información mínima para la capa superior: el SID de la verificación y el
/// estado devuelto por Twilio (normalmente <c>"pending"</c>). Nunca contiene
/// el código OTP (Twilio lo genera y lo valida del lado del proveedor).
/// </summary>
public record TwilioSendOtpResult(
    string? VerificationSid,
    string Status);

/// <summary>
/// Resultado de la verificación de un código contra Twilio Verify V2.
/// <see cref="IsApproved"/> es true únicamente cuando Twilio devuelve
/// <c>status = "approved"</c>. Un código incorrecto NO lanza excepción:
/// se representa con <see cref="IsApproved"/> = false y el status real.
/// </summary>
public record TwilioCheckOtpResult(
    bool IsApproved,
    string Status);

/// <summary>
/// Abstracción de Twilio Verify V2 para OTP por SMS. Oculta por completo el
/// SDK de Twilio a la capa superior (no expone tipos de Twilio en el
/// contrato) y devuelve tipos propios del proyecto.
/// </summary>
public interface ITwilioOtpService
{
    /// <summary>
    /// Solicita el envío de una verificación por SMS a Twilio Verify.
    /// <paramref name="phoneNumber"/> debe estar en formato E.164
    /// (ej. <c>+15765550100</c>); la conversión desde el formato interno de
    /// CoppAddresd es responsabilidad del llamador (OtpService).
    /// </summary>
    Task<TwilioSendOtpResult> SendAsync(string phoneNumber, CancellationToken ct = default);

    /// <summary>
    /// Verifica un código OTP contra Twilio Verify.
    /// <paramref name="phoneNumber"/> debe estar en formato E.164.
    /// Un código incorrecto devuelve <see cref="TwilioCheckOtpResult.IsApproved"/>
    /// = false (resultado de negocio esperado), no lanza excepción.
    /// </summary>
    Task<TwilioCheckOtpResult> CheckAsync(string phoneNumber, string code, CancellationToken ct = default);
}
