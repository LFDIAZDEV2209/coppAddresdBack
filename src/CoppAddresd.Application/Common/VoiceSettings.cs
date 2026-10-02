namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración del canal de voz (llamada TTS al contacto de emergencia SOS).
/// <see cref="Provider"/> elige la implementación registrada en DI: <c>Noop</c>
/// (log en desarrollo, default) o <c>Twilio</c> (llamadas con TwiML
/// <c>&lt;Say&gt;</c>). Las credenciales de Twilio son secretos: vienen de
/// configuración gitignoreada / variables de entorno (<c>VOICE__*</c> o
/// <c>Voice:*</c>) o del gestor de secretos, nunca del código.
///
/// Contrato fail-soft: con <c>Provider=Twilio</c> pero configuración incompleta
/// la API arranca igual (se registra <c>NoOpVoiceCaller</c>) y el canal de voz
/// se reporta <c>NoConfigurado</c>; el arranque NUNCA falla por voz no
/// configurada.
/// </summary>
public class VoiceSettings
{
    public const string SectionName = "Voice";

    /// <summary>Proveedor activo: <c>Noop</c> (default) o <c>Twilio</c>.</summary>
    public string Provider { get; set; } = "Noop";

    /// <summary>
    /// Habilitador explícito del proveedor real (killswitch). Con <c>false</c>
    /// (default) <c>TwilioVoiceCaller.IsConfigured</c> es false y el canal se
    /// reporta <c>NoConfigurado</c>; el flujo llamador nunca se rompe.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>Account SID de Twilio (ej. <c>ACxxxxxxxx</c>).</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>
    /// Auth Token de la cuenta de Twilio (misma credencial que Twilio Messages:
    /// las llamadas del repo se autentican con el Auth Token de la cuenta).
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Remitente por defecto (E.164) para llamadas salientes. Debe ser un
    /// número comprado con capacidad de VOZ (el mismo del SMS puede servir).
    /// </summary>
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>
    /// Indica si hay credenciales suficientes para llamar realmente:
    /// habilitado + AccountSid/AuthToken + FromNumber.
    /// </summary>
    public bool IsConfigured =>
        IsEnabled
        && !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !string.IsNullOrWhiteSpace(FromNumber);
}
