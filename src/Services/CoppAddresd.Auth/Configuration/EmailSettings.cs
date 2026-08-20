namespace CoppAddresd.Auth.Configuration;

/// <summary>
/// Configuración del envío de correos transaccionales (invitaciones).
/// <c>Provider</c>: "Log" (dev: imprime el correo en el logger) o "Smtp"
/// (producción). <c>FrontendUrl</c> se usa para construir los enlaces de
/// invitación que llegan al profesional.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Log";

    public string From { get; set; } = "no-reply@coppaddresd.com";

    public string FromName { get; set; } = "CoppAddresd";

    /// <summary>Base URL del frontend para enlaces (ej. http://localhost:3000).</summary>
    public string FrontendUrl { get; set; } = "http://localhost:3000";

    // SMTP (usado solo con Provider = "Smtp").
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    public bool SmtpUseSsl { get; set; } = true;
}
