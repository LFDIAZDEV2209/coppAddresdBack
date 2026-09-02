using CoppAddresd.Application.DTOs.Email;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Service contract for sending email notifications.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email using the specified email message details.
    /// </summary>
    /// <param name="message">The email message details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendEmailAsync(EmailMessageDto message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience method to send a simple email to a single recipient.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates if body is HTML (default true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);
}
