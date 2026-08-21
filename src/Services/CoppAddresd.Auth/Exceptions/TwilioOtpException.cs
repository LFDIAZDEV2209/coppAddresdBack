namespace CoppAddresd.Auth.Exceptions;

/// <summary>
/// Clasificación del error de la integración Twilio Verify. La fase de HTTP
/// (controller/middleware) la usa para decidir el status code y el mensaje
/// expuesto al usuario sin filtrar detalles internos de Twilio.
/// </summary>
public enum TwilioOtpErrorKind
{
    /// <summary>Twilio no está habilitado (Twilio:IsEnabled = false).</summary>
    Disabled,

    /// <summary>Configuración inválida (p. ej. VerifyServiceSid vacío).</summary>
    InvalidConfiguration,

    /// <summary>Teléfono con formato no válido (debe ser E.164).</summary>
    InvalidPhone,

    /// <summary>Parámetro inválido (p. ej. código OTP vacío).</summary>
    InvalidParameter,

    /// <summary>Twilio no disponible / fallo de red o del proveedor.</summary>
    ProviderUnavailable,

    /// <summary>Twilio rechazó la petición por límite de tasa (429).</summary>
    RateLimited,

    /// <summary>Error genérico devuelto por Twilio.</summary>
    ProviderError
}

/// <summary>
/// Error tipado de la integración con Twilio Verify V2. Sigue el patrón de
/// <c>AiServiceException</c> (API principal): conserva solo la información
/// necesaria para que la capa HTTP posterior pueda mapear el error
/// (kind + código de error de Twilio + status code) sin exponer secretos,
/// stack trace ni detalles internos del proveedor.
/// </summary>
public sealed class TwilioOtpException : Exception
{
    public TwilioOtpException(
        TwilioOtpErrorKind kind,
        string message,
        int? twilioStatusCode = null,
        int? twilioCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        TwilioStatusCode = twilioStatusCode;
        TwilioCode = twilioCode;
    }

    /// <summary>Clasificación del error para mapeo a HTTP.</summary>
    public TwilioOtpErrorKind Kind { get; }

    /// <summary>Status HTTP devuelto por Twilio (si aplica).</summary>
    public int? TwilioStatusCode { get; }

    /// <summary>Código de error de Twilio (p. ej. 60200), si aplica.</summary>
    public int? TwilioCode { get; }
}
