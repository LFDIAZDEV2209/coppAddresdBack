namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración del canal SMS (SPEC A13). El proveedor real todavía no está
/// integrado: <see cref="Provider"/> elige <c>Noop</c> (log en desarrollo) o
/// <c>Twilio</c> cuando se implemente.
/// </summary>
public class SmsSettings
{
    public const string SectionName = "Sms";

    /// <summary>Proveedor activo: <c>Noop</c> (default) o <c>Twilio</c>.</summary>
    public string Provider { get; set; } = "Noop";

    /// <summary>Remitente por defecto (E.164) para mensajes salientes.</summary>
    public string FromNumber { get; set; } = string.Empty;
}
