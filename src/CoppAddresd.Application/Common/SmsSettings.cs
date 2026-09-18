namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración del canal SMS (SPEC A13). <see cref="Provider"/> elige la
/// implementación registrada en DI: <c>Noop</c> (log en desarrollo, default) o
/// <c>Twilio</c> (Twilio Messages, envío real). Las credenciales de Twilio son
/// secretos: vienen de configuración gitignoreada / variables de entorno
/// (<c>TWILIO__*</c> o <c>Sms:*</c>) o del gestor de secretos, nunca del código.
///
/// Contrato fail-soft: con <c>Provider=Twilio</c> pero configuración incompleta
/// la API arranca igual (se registra <c>NoOpSmsSender</c>) y cada envío deja un
/// Warning; el arranque NUNCA falla por SMS no configurado.
/// </summary>
public class SmsSettings
{
    public const string SectionName = "Sms";

    /// <summary>Proveedor activo: <c>Noop</c> (default) o <c>Twilio</c>.</summary>
    public string Provider { get; set; } = "Noop";

    /// <summary>
    /// Habilitador explícito del proveedor real (killswitch). Con <c>false</c>
    /// (default) <c>TwilioSmsSender.IsConfigured</c> es false y el canal se
    /// reporta como <c>disabled</c>; el flujo llamador nunca se rompe.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>Account SID de Twilio (ej. <c>ACxxxxxxxx</c>).</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>
    /// Auth Token de la cuenta de Twilio. Twilio Messages lo usa como
    /// credencial (a diferencia de Verify/Video, que usan API Key).
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Remitente por defecto (E.164) para mensajes salientes.</summary>
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>
    /// Messaging Service SID (ej. <c>MGxxxxxxxx</c>) alternativo al
    /// <see cref="FromNumber"/>: Twilio elige el remitente óptimo del pool.
    /// Si ambos están configurados, se envía el <c>FromNumber</c> con el
    /// servicio asociado.
    /// </summary>
    public string MessagingServiceSid { get; set; } = string.Empty;

    /// <summary>
    /// Indica si hay credenciales suficientes para enviar realmente: habilitado
    /// + AccountSid/AuthToken + (<see cref="FromNumber"/> o
    /// <see cref="MessagingServiceSid"/>).
    /// </summary>
    public bool IsConfigured =>
        IsEnabled
        && !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && (!string.IsNullOrWhiteSpace(FromNumber)
            || !string.IsNullOrWhiteSpace(MessagingServiceSid));
}
