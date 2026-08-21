namespace CoppAddresd.Telemedicine.Application.Configuration;

/// <summary>
/// Configuración operativa del microservicio consumida por la capa de
/// aplicación (la infraestructura decide de dónde sale: appsettings, variables
/// de entorno...). Distinta de <c>TelemedicineSettings</c> (reglas de negocio
/// por organización/clínica persistidas en BD).
/// </summary>
public sealed class TelemedicineOptions
{
    public const string SectionName = "Telemedicine";

    /// <summary>URL pública del endpoint de webhooks del proveedor (StatusCallback de las salas Twilio).</summary>
    public string WebhookUrl { get; set; } = string.Empty;
}
