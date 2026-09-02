using CoppAddresd.Application.DTOs.Email;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services.Email;

/// <summary>
/// Development / fallback implementation of <see cref="IEmailService"/> that logs
/// email details to <see cref="ILogger"/> without attempting actual SMTP network delivery.
/// </summary>
public sealed class LogEmailService : IEmailService
{
    private readonly ILogger<LogEmailService> _logger;
    private readonly EmailOptions _options;

    public LogEmailService(
        ILogger<LogEmailService> logger,
        IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendEmailAsync(EmailMessageDto message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Email sending is disabled by configuration (Email:Enabled = false). Email to {Recipients} skipped.",
                string.Join(", ", message.To));
            return Task.CompletedTask;
        }

        if (message.To.Count == 0)
        {
            throw new ArgumentException("At least one primary recipient ('To') must be specified.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new ArgumentException("Email subject cannot be empty.", nameof(message));
        }

        var senderAddress = string.IsNullOrWhiteSpace(message.FromAddress) ? _options.FromAddress : message.FromAddress;
        var senderName = string.IsNullOrWhiteSpace(message.FromName) ? _options.FromName : message.FromName;

        _logger.LogInformation(
            "[LOG-EMAIL-SERVICE] SIMULATED EMAIL SENT\n" +
            "  From: \"{SenderName}\" <{SenderAddress}>\n" +
            "  To: {To}\n" +
            "  Cc: {Cc}\n" +
            "  Bcc: {Bcc}\n" +
            "  Subject: {Subject}\n" +
            "  IsHtml: {IsHtml}\n" +
            "  Attachments: {AttachmentCount}\n" +
            "  Body Snippet: {BodySnippet}",
            senderName,
            senderAddress,
            string.Join(", ", message.To),
            message.Cc.Count > 0 ? string.Join(", ", message.Cc) : "(none)",
            message.Bcc.Count > 0 ? string.Join(", ", message.Bcc) : "(none)",
            message.Subject,
            message.IsHtml,
            message.Attachments.Count,
            message.Body.Length > 200 ? string.Concat(message.Body.AsSpan(0, 200), "...") : message.Body);

        return Task.CompletedTask;
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
