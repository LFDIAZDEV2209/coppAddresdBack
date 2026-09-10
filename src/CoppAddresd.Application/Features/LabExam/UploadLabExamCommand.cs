using CoppAddresd.Application.DTOs.LabExam;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.LabExam;

/// <summary>
/// Comando para procesar la subida de un examen de laboratorio (imagen o PDF).
/// </summary>
public record UploadLabExamCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileLength,
    Guid PatientId,
    string? ThreadId = null) : IRequest<LabExamUploadResult>;

/// <summary>
/// Validador de reglas para la subida de exámenes de laboratorio.
/// </summary>
public class UploadLabExamCommandValidator : AbstractValidator<UploadLabExamCommand>
{
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "application/pdf"
    };

    public const long MaxRawSizeBytes = 20 * 1024 * 1024; // 20 MB

    public UploadLabExamCommandValidator()
    {
        RuleFor(x => x.FileStream)
            .NotNull()
            .WithMessage("File stream is required.");

        RuleFor(x => x.FileLength)
            .GreaterThan(0)
            .WithMessage("File cannot be empty.")
            .LessThanOrEqualTo(MaxRawSizeBytes)
            .WithMessage("File exceeds the 20 MB limit.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required.")
            .Must(fn => !string.IsNullOrWhiteSpace(fn) && AllowedExtensions.Contains(Path.GetExtension(fn)))
            .WithMessage("Unsupported file type. Please upload a JPEG, PNG, or PDF.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .WithMessage("Content type is required.")
            .Must(ct => !string.IsNullOrWhiteSpace(ct) && AllowedContentTypes.Contains(ct.Split(';')[0].Trim()))
            .WithMessage("Unsupported file type. Please upload a JPEG, PNG, or PDF.");

        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("Patient ID is required.");
    }
}
