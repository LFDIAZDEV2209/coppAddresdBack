namespace CoppAddresd.Application.Features.Sos;

/// <summary>
/// Configuración general del módulo SOS emergency dispatch.
/// </summary>
public sealed class SosOptions
{
    public const string SectionName = "Sos";

    /// <summary>Número de emergencia a incluir en el mensaje (default "911").</summary>
    public string EmergencyNumber { get; set; } = "911";

    /// <summary>Proveedor de SMS: "Log" (default) o "Twilio".</summary>
    public string SmsProvider { get; set; } = "Log";

    /// <summary>Proveedor de email: "Log" (default) o "Smtp" (reutiliza el servicio existente).</summary>
    public string EmailProvider { get; set; } = "Log";

    /// <summary>Proveedor de voz: "Log" (default) o "Twilio".</summary>
    public string VoiceProvider { get; set; } = "Log";
}
