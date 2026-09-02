namespace CoppAddresd.Application.DTOs.Email;

/// <summary>
/// Data transfer object representing an email message to be sent.
/// </summary>
public sealed class EmailMessageDto
{
    /// <summary>
    /// List of primary recipient email addresses.
    /// </summary>
    public List<string> To { get; set; } = [];

    /// <summary>
    /// Optional list of CC recipient email addresses.
    /// </summary>
    public List<string> Cc { get; set; } = [];

    /// <summary>
    /// Optional list of BCC recipient email addresses.
    /// </summary>
    public List<string> Bcc { get; set; } = [];

    /// <summary>
    /// Subject line of the email.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Body content of the email (HTML or plain text).
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the body content contains HTML. Default is true.
    /// </summary>
    public bool IsHtml { get; set; } = true;

    /// <summary>
    /// Optional custom sender address. If empty, the default configured sender is used.
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>
    /// Optional custom sender display name. If empty, the default configured sender name is used.
    /// </summary>
    public string? FromName { get; set; }

    /// <summary>
    /// Optional list of file attachments.
    /// </summary>
    public List<EmailAttachmentDto> Attachments { get; set; } = [];
}
