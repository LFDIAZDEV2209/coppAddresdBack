using CoppAddresd.Application.DTOs.Email;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services.Email;

/// <summary>
/// Implementación de desarrollo: registra solo metadatos no personales y no
/// realiza entrega SMTP. Nunca debe representar un canal SOS configurado.
/// </summary>
public sealed class LogEmailService : IEmailService
{
    private readonly ILogger<LogEmailService> _logger;
    private readonly EmailOptions _options;

    public bool IsConfigured => false;

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
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
        {
            _logger.LogInformation("Envío de correo deshabilitado por configuración.");
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

        _logger.LogInformation(
            "Correo de desarrollo registrado sin entrega real: recipients={RecipientCount}, html={IsHtml}, attachments={AttachmentCount}.",
            message.To.Count,
            message.IsHtml,
            message.Attachments.Count);

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
