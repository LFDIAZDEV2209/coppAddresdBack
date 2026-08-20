namespace CoppAddresd.Auth.Interfaces;

/// <summary>Representa un correo a enviar.</summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);

/// <summary>
/// Envío de correos transaccionales. Implementaciones: "Log" (dev, imprime en
/// el logger) y "Smtp" (producción). En producción (AWS) migrar a SES detrás
/// de esta misma interfaz — sin tocar los consumidores.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
