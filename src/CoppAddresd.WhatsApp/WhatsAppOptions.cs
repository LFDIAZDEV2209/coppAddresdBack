namespace CoppAddresd.WhatsApp;

/// <summary>Credenciales exclusivamente del entorno; nunca se enlazan desde appsettings.</summary>
public sealed class WhatsAppOptions
{
    public bool Enabled { get; init; }
    public string AccessToken { get; init; } = "";
    public string PhoneNumberId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string AppSecret { get; init; } = "";
    public string WebhookVerifyToken { get; init; } = "";

    public static WhatsAppOptions FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        return new()
        {
            Enabled = read("WHATSAPP_ENABLED") is "1" || bool.TryParse(read("WHATSAPP_ENABLED"), out var enabled) && enabled,
            AccessToken = read("WHATSAPP_ACCESS_TOKEN") ?? "",
            PhoneNumberId = read("WHATSAPP_PHONE_NUMBER_ID") ?? "",
            AppId = read("WHATSAPP_APP_ID") ?? "",
            AppSecret = read("WHATSAPP_APP_SECRET") ?? "",
            WebhookVerifyToken = read("WHATSAPP_WEBHOOK_VERIFY_TOKEN") ?? "",
        };
    }

    public void Validate()
    {
        if (Enabled && new[] { AccessToken, PhoneNumberId, AppId, AppSecret, WebhookVerifyToken }.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Faltan variables de entorno obligatorias del canal WhatsApp.");
    }
}
