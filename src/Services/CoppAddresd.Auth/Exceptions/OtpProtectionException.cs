using CoppAddresd.Auth.Interfaces;

namespace CoppAddresd.Auth.Exceptions;

/// <summary>
/// Bloqueo del motor de protección OTP (límite de envío o verificación). Se
/// traduce a HTTP 429 por el middleware global con el mismo shape que usa el
/// rate limiter existente. La razón es interna (para logging) y NUNCA se
/// expone al cliente.
/// </summary>
public sealed class OtpProtectionException : Exception
{
    public OtpProtectionException(
        OtpProtectionReason reason,
        string message,
        int? retryAfterSeconds = null)
        : base(message)
    {
        Reason = reason;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>Razón interna del bloqueo (IP/teléfono/documento/lockout).</summary>
    public OtpProtectionReason Reason { get; }

    /// <summary>Segundos sugeridos antes de reintentar, si están disponibles.</summary>
    public int? RetryAfterSeconds { get; }
}