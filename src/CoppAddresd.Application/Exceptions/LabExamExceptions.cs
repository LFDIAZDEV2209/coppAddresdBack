using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Exceptions;

/// <summary>
/// Excepción lanzada cuando el archivo (post-compresión o el original en fallback)
/// supera el límite de 10 MB. Se mapea a HTTP 422.
/// </summary>
public sealed class LabExamTooLargeException : UnprocessableEntityException
{
    public const string DefaultMessage = "Compressed file still exceeds 10 MB. Please reduce the document size or split it into pages.";

    public LabExamTooLargeException(string message = DefaultMessage)
        : base(message)
    {
    }
}

/// <summary>
/// Excepción lanzada cuando la librería de compresión falla o el archivo está corrupto.
/// Se mapea a HTTP 422.
/// </summary>
public sealed class FileCompressionException : UnprocessableEntityException
{
    public const string DefaultMessage = "The document could not be processed. Please check the file and try again.";

    public FileCompressionException(string message = DefaultMessage, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Excepción lanzada cuando el formato de archivo o MIME type no es soportado.
/// Se mapea a HTTP 422.
/// </summary>
public sealed class UnsupportedLabExamFileTypeException : UnprocessableEntityException
{
    public const string DefaultMessage = "Unsupported file type. Please upload a JPEG, PNG, or PDF.";

    public UnsupportedLabExamFileTypeException(string message = DefaultMessage)
        : base(message)
    {
    }
}

/// <summary>
/// Excepción lanzada cuando el archivo raw supera el límite de 20 MB.
/// Se mapea a HTTP 422.
/// </summary>
public sealed class LabExamRawFileTooLargeException : UnprocessableEntityException
{
    public const string DefaultMessage = "File exceeds the 20 MB limit.";

    public LabExamRawFileTooLargeException(string message = DefaultMessage)
        : base(message)
    {
    }
}
