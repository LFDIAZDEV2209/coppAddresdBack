using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Envía correos vía SMTP (producción). Las credenciales y host vienen de
/// <c>Email</c> (Secrets Manager en AWS, nunca commitear valores reales).
/// </summary>
public class SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = settings.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        using var smtp = new SmtpClient
        {
            Host = _settings.SmtpHost,
            Port = _settings.SmtpPort,
            EnableSsl = _settings.SmtpUseSsl,
            Credentials = string.IsNullOrWhiteSpace(_settings.SmtpUsername)
                ? null
                : new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword),
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_settings.From, _settings.FromName),
            To = { message.To },
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };

        if (!string.IsNullOrWhiteSpace(message.PlainTextBody))
        {
            var view = AlternateView.CreateAlternateViewFromString(message.PlainTextBody, null, "text/plain");
            mail.AlternateViews.Add(view);
        }

        await smtp.SendMailAsync(mail, ct);
        logger.LogInformation("Correo enviado a {To} (asunto: {Subject})", message.To, message.Subject);
    }
}

/// <summary>
/// Provider "Log" (dev): no envía el correo, lo imprime en el logger con el
/// enlace completo. Permite probar el flujo de invitación sin infraestructura.
/// </summary>
public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL {Provider}] Para: {To} | Asunto: {Subject}\n{Body}",
            "LOG", message.To, message.Subject, message.HtmlBody);
        return Task.CompletedTask;
    }
}
