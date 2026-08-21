namespace CoppAddresd.Auth.Configuration;

/// <summary>
/// Configuración de seguridad específica del flujo OTP (send-otp / verify-otp).
/// Define los límites por IP, por teléfono y por documento, el cooldown de
/// reenvío y los intentos fallidos de verificación para el canal PHONE.
///
/// NOTA: en esta etapa la configuración existe pero el mecanismo de protección
/// todavía NO está implementado (se hará en una fase posterior). Los valores
/// pueden sobreescribirse con variables de entorno usando la convención
/// estándar de ASP.NET Core (sección con '__'): por ejemplo
/// <c>OTPSECURITY__SEND_PER_PHONE_PER_MINUTE</c>.
/// </summary>
public class OtpSecuritySettings
{
    public const string SectionName = "OtpSecurity";

    /// <summary>
    /// Habilita/deshabilita las protecciones de seguridad del flujo OTP.
    /// Recomendado: true. (El comportamiento aún no se implementa.)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Límite de envíos OTP por dirección IP por minuto (SEND).</summary>
    public int SendPerIpPerMinute { get; set; } = 5;

    /// <summary>Límite de envíos OTP por dirección IP por hora (SEND).</summary>
    public int SendPerIpPerHour { get; set; } = 30;

    /// <summary>Límite de envíos OTP por teléfono (E.164) por minuto (SEND).</summary>
    public int SendPerPhonePerMinute { get; set; } = 3;

    /// <summary>Límite de envíos OTP por teléfono (E.164) por hora (SEND).</summary>
    public int SendPerPhonePerHour { get; set; } = 10;

    /// <summary>Límite de envíos OTP por teléfono (E.164) por día (SEND).</summary>
    public int SendPerPhonePerDay { get; set; } = 20;

    /// <summary>
    /// Cooldown en segundos entre envíos OTP al mismo teléfono (SEND).
    /// 0 desactiva el cooldown.
    /// </summary>
    public int SendPhoneCooldownSeconds { get; set; } = 60;

    /// <summary>Límite de envíos OTP por número de documento por hora (SEND).</summary>
    public int SendPerDocumentPerHour { get; set; } = 5;

    /// <summary>Límite de verificaciones OTP por dirección IP por minuto (VERIFY).</summary>
    public int VerifyPerIpPerMinute { get; set; } = 30;

    /// <summary>Ventana en minutos para el límite de verificaciones por teléfono (VERIFY).</summary>
    public int VerifyPerPhoneWindowMinutes { get; set; } = 5;

    /// <summary>Límite de verificaciones OTP por teléfono dentro de la ventana (VERIFY).</summary>
    public int VerifyPerPhoneLimit { get; set; } = 10;

    /// <summary>
    /// Máximo de intentos fallidos de verificación por teléfono antes del
    /// lockout (VERIFY, canal PHONE).
    /// </summary>
    public int VerifyPhoneMaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Lockout en segundos tras superar los intentos fallidos (VERIFY, canal
    /// PHONE). 0 desactiva el lockout.
    /// </summary>
    public int VerifyPhoneLockoutSeconds { get; set; } = 300;
}
