namespace CoppAddresd.Infrastructure.Services.Email;

/// <summary>
/// Configuration options for the Email service.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Email provider type: "Log" (local mock/logging), "Smtp" (standard SMTP / Outlook), or "None".
    /// Default is "Log".
    /// </summary>
    public string Provider { get; set; } = "Log";

    /// <summary>
    /// Master toggle to enable or disable email sending. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// SMTP Server hostname (e.g., "smtp.office365.com" for Outlook / Office 365).
    /// </summary>
    public string SmtpHost { get; set; } = "smtp.office365.com";

    /// <summary>
    /// SMTP Server port. Default is 587 (STARTTLS).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Enable SSL / TLS encryption. Default is true.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// SMTP username / email address for authentication.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password / app password for authentication.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Default sender email address.
    /// </summary>
    public string FromAddress { get; set; } = "noreply@coppaddresd.com";

    /// <summary>
    /// Default sender display name.
    /// </summary>
    public string FromName { get; set; } = "COPP-ADRESD";

    /// <summary>
    /// Timeout in milliseconds for SMTP operations. Default is 10000ms (10 seconds).
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;
}
