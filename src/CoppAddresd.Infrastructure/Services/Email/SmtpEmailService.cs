using System.Net;
using System.Net.Mail;
using System.Text;
using CoppAddresd.Application.DTOs.Email;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services.Email;

/// <summary>
/// SMTP implementation of <see cref="IEmailService"/> supporting standard SMTP hosts
/// including Microsoft Outlook / Office 365 (<c>smtp.office365.com:587</c>).
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly EmailOptions _options;

    public SmtpEmailService(
        ILogger<SmtpEmailService> logger,
        IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task SendEmailAsync(EmailMessageDto message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Email sending is disabled (Email:Enabled = false). Skipping email to {Recipients}.",
                string.Join(", ", message.To));
            return;
        }

        if (message.To.Count == 0)
        {
            throw new ArgumentException("At least one primary recipient ('To') must be specified.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new ArgumentException("Email subject cannot be empty.", nameof(message));
        }

        // Defensive check: If credentials are not configured, log a warning and fall back safely
        if (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning(
                "SMTP credentials are not configured in EmailOptions (UserName or Password is empty). " +
                "Falling back to log-only delivery for email to {Recipients} with subject '{Subject}'.",
                string.Join(", ", message.To), message.Subject);

            _logger.LogInformation(
                "[SMTP-FALLBACK-LOG] Email Content:\n" +
                "  To: {To}\n" +
                "  Subject: {Subject}\n" +
                "  Body Snippet: {BodySnippet}",
                string.Join(", ", message.To),
                message.Subject,
                message.Body.Length > 200 ? string.Concat(message.Body.AsSpan(0, 200), "...") : message.Body);

            return;
        }

        var senderAddress = string.IsNullOrWhiteSpace(message.FromAddress) ? _options.FromAddress : message.FromAddress;
        var senderName = string.IsNullOrWhiteSpace(message.FromName) ? _options.FromName : message.FromName;

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(senderAddress, senderName, Encoding.UTF8),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsHtml,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        foreach (var recipient in message.To)
        {
            mailMessage.To.Add(recipient);
        }

        foreach (var cc in message.Cc)
        {
            mailMessage.CC.Add(cc);
        }

        foreach (var bcc in message.Bcc)
        {
            mailMessage.Bcc.Add(bcc);
        }

        foreach (var attachment in message.Attachments)
        {
            var stream = new MemoryStream(attachment.Content);
            var mailAttachment = new Attachment(stream, attachment.FileName, attachment.ContentType);
            mailMessage.Attachments.Add(mailAttachment);
        }

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.UserName, _options.Password),
            Timeout = _options.TimeoutMs
        };

        try
        {
            _logger.LogInformation("Sending email via SMTP ({Host}:{Port}) to {Recipients} with subject '{Subject}'...",
                _options.SmtpHost, _options.SmtpPort, string.Join(", ", message.To), message.Subject);

            await client.SendMailAsync(mailMessage, cancellationToken);

            _logger.LogInformation("Successfully sent email via SMTP to {Recipients}.", string.Join(", ", message.To));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP host '{Host}:{Port}' to {Recipients}.",
                _options.SmtpHost, _options.SmtpPort, string.Join(", ", message.To));
            throw;
        }
    }

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException("Recipient address ('to') cannot be empty.", nameof(to));
        }

        var message = new EmailMessageDto
        {
            To = [to],
            Subject = subject,
            Body = body,
            IsHtml = isHtml
        };

        return SendEmailAsync(message, cancellationToken);
    }
}
