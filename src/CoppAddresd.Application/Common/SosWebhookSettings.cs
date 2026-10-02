namespace CoppAddresd.Application.Common;

/// <summary>
/// Base pública (túnel o despliegue) para los callbacks de estado de Twilio
/// del flujo SOS. Si está vacía no se envían callbacks y los canales solo
/// reflejan el estado "aceptado" (comportamiento anterior).
/// Config: "SosWebhook": { "BaseUrl": "https://xxx.trycloudflare.com" }
/// </summary>
public sealed class SosWebhookSettings
{
    public const string SectionName = "SosWebhook";

    /// <summary>URL base sin slash final, p. ej. https://xxx.trycloudflare.com.</summary>
    public string? BaseUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>Base normalizada sin slash final (null si no configurada).</summary>
    public string? NormalizedBaseUrl =>
        string.IsNullOrWhiteSpace(BaseUrl) ? null : BaseUrl.Trim().TrimEnd('/');
}
