namespace CoppAddresd.Auth.Models;

/// <summary>
/// Respuesta al envío de un OTP. El código real nunca viaja en el cuerpo en
/// producción; <see cref="DevCode"/> solo se devuelve en entornos de desarrollo
/// para permitir pruebas end-to-end sin un proveedor de correo/SMS real.
/// </summary>
public record SendOtpResponse(
    int ExpiresInSeconds,
    string? DevCode = null);
