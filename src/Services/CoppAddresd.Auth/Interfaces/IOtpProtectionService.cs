namespace CoppAddresd.Auth.Interfaces;

/// <summary>
/// Razón por la que una operación OTP (envío o verificación) fue bloqueada por
/// el motor de protección. No expone información sensible; la capa HTTP decide
/// cómo traducirla a un mensaje seguro.
/// </summary>
public enum OtpProtectionReason
{
    /// <summary>Límite de envíos por IP alcanzado (ventana por minuto u hora).</summary>
    SendIpRateLimit,

    /// <summary>Límite de envíos por teléfono alcanzado (minuto, hora o día).</summary>
    SendPhoneRateLimit,

    /// <summary>Cooldown de reenvío al mismo teléfono aún activo.</summary>
    SendPhoneCooldown,

    /// <summary>Límite de envíos por documento alcanzado (por hora).</summary>
    SendDocumentRateLimit,

    /// <summary>Límite de verificaciones por IP alcanzado (por minuto).</summary>
    VerifyIpRateLimit,

    /// <summary>Límite de verificaciones por teléfono alcanzado dentro de la ventana.</summary>
    VerifyPhoneRateLimit,

    /// <summary>Teléfono temporalmente bloqueado por demasiados intentos fallidos.</summary>
    VerifyPhoneLocked,
}

/// <summary>
/// Resultado tipado de una evaluación del motor de protección OTP. No se usan
/// excepciones para los bloqueos normales de negocio: <see cref="Allowed"/>
/// indica si la operación puede continuar, <see cref="Reason"/> por qué fue
/// bloqueada y <see cref="RetryAfterSeconds"/> cuánto esperar antes de
/// reintentar (si aplica).
/// </summary>
public sealed record OtpProtectionResult(
    bool Allowed,
    OtpProtectionReason? Reason,
    int? RetryAfterSeconds)
{
    /// <summary>Operación permitida.</summary>
    public static OtpProtectionResult Allow() => new(true, null, null);

    /// <summary>Operación bloqueada con la razón y, opcionalmente, la espera.</summary>
    public static OtpProtectionResult Block(OtpProtectionReason reason, int? retryAfterSeconds = null)
        => new(false, reason, retryAfterSeconds);
}

/// <summary>
/// Motor interno de protección del flujo OTP (estado en memoria compartido
/// dentro de la instancia). Controla límites de envío por IP/teléfono/documento,
/// cooldown de reenvío y límites/lockout de verificación para el canal PHONE.
///
/// Independiente de HttpContext, AuthController, el SDK de Twilio, OtpService y
/// la base de datos: recibe los identificadores explícitamente. La integración
/// con el flujo HTTP se realiza en una fase posterior.
/// </summary>
public interface IOtpProtectionService
{
    /// <summary>
    /// Evalúa si un nuevo envío OTP está permitido para la combinación de IP,
    /// documento y teléfono. NO consume cuota: la cuota solo se registra con
    /// <see cref="RegisterSend"/>, que debe llamarse únicamente cuando el
    /// proveedor (p. ej. Twilio) aceptó el envío.
    /// </summary>
    OtpProtectionResult CheckCanSend(string ipAddress, string documentNumber, string phoneE164);

    /// <summary>
    /// Registra un envío OTP realizado (una sola vez, tras la aceptación del
    /// proveedor). Actualiza los contadores de las ventanas por IP, teléfono y
    /// documento, y el cooldown por teléfono.
    /// </summary>
    void RegisterSend(string ipAddress, string documentNumber, string phoneE164);

    /// <summary>
    /// Evalúa si un intento de verificación OTP está permitido (bloqueado por
    /// lockout, por límite de IP o por límite de teléfono dentro de la ventana).
    /// NO consume cuota: los intentos completados se registran con
    /// <see cref="RegisterVerifyFailed"/> o <see cref="RegisterVerifySucceeded"/>.
    /// </summary>
    OtpProtectionResult CheckCanVerify(string ipAddress, string phoneE164);

    /// <summary>
    /// Registra un intento de verificación con código incorrecto: incrementa los
    /// contadores y los intentos fallidos del teléfono; al alcanzar
    /// <c>VerifyPhoneMaxFailedAttempts</c> activa el lockout temporal.
    /// </summary>
    void RegisterVerifyFailed(string ipAddress, string phoneE164);

    /// <summary>
    /// Registra una verificación aprobada: incrementa los contadores de la
    /// ventana y limpia los intentos fallidos y el lockout del teléfono.
    /// </summary>
    void RegisterVerifySucceeded(string ipAddress, string phoneE164);
}
