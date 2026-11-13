namespace CoppAddresd.Infrastructure.Sos;
public sealed class SosWhatsAppSettings
{
    public bool Enabled { get; set; }
    public string TemplateName { get; set; } = "copp_sos_alert_v1";
    public string Language { get; set; } = "es";
}
