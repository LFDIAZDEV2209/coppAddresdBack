namespace CoppAddresd.Application.DTOs.Email;

/// <summary>
/// Represents an email attachment.
/// </summary>
public sealed class EmailAttachmentDto
{
    /// <summary>
    /// File name including extension (e.g., "report.pdf").
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Content bytes of the attachment.
    /// </summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// MIME content type (e.g., "application/pdf", "image/png").
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";
}
