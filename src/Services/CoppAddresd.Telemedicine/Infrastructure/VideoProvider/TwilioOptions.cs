namespace CoppAddresd.Telemedicine.Infrastructure.VideoProvider;

/// <summary>
/// Configuración de Twilio. Secretos: NUNCA en código; provienen de
/// configuración gitignoreada / variables de entorno / secrets manager.
/// </summary>
public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;

    /// <summary>API Key (región US1 obligatoria para Video).</summary>
    public string ApiKeySid { get; set; } = string.Empty;

    public string ApiKeySecret { get; set; } = string.Empty;

    /// <summary>Auth Token de la cuenta: se usa para validar la firma de webhooks.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Si <c>false</c> (solo desarrollo) se omite la validación de firma de
    /// webhook. En producción DEBE ser <c>true</c> (default seguro).
    /// </summary>
    public bool ValidateWebhookSignature { get; set; } = true;

    /// <summary>Región de la cuenta (us1 para Video por defecto).</summary>
    public string Region { get; set; } = "us1";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(ApiKeySid)
        && !string.IsNullOrWhiteSpace(ApiKeySecret);
}
