using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>
/// Genera la clave determinista del storage (<c>documents/{patientId}/{guid}{ext}</c>)
/// para una futura subida. Valida el paciente, el tipo de documento y la extensión
/// ANTES de que el frontend suba el binario (evita basura en el storage).
/// </summary>
public record CreateDocumentUploadIntentCommand(
    Guid PatientId,
    Guid DocumentTypeId,
    string FileName,
    Guid? ParentDocumentId)
    : IRequest<DocumentUploadIntentResult>;

public sealed class CreateDocumentUploadIntentCommandHandler(
    IDocumentRepository repository) : IRequestHandler<CreateDocumentUploadIntentCommand, DocumentUploadIntentResult>
{
    public async Task<DocumentUploadIntentResult> Handle(
        CreateDocumentUploadIntentCommand request,
        CancellationToken ct)
    {
        if (!await repository.PatientExistsAsync(request.PatientId, ct))
            throw new NotFoundException("El paciente no existe.");

        var type = await repository.GetTypeByIdAsync(request.DocumentTypeId, ct);
        if (type is null || !type.IsActive)
            throw new UnprocessableEntityException("El tipo de documento no existe o está inactivo.");

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        ValidateExtension(type, extension);

        if (request.ParentDocumentId is { } parentId)
        {
            var root = await repository.GetRootAsync(parentId, ct);
            if (root is null)
                throw new NotFoundException("El documento raíz de la nueva versión no existe.");
        }

        var storageKey = $"documents/{request.PatientId}/{Guid.NewGuid():N}{extension}";
        var expiresInSeconds = (int)TimeSpan.FromMinutes(15).TotalSeconds;

        return new DocumentUploadIntentResult(storageKey, expiresInSeconds);
    }

    private static void ValidateExtension(Domain.Entities.ClinicalDocumentType type, string extension)
    {
        if (type.AllowedExtensions.Count == 0)
            return;

        var allowed = extension.TrimStart('.');
        if (!type.AllowedExtensions.Contains(allowed, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleViolationException(
                $"El tipo '{type.Name}' no admite archivos .{allowed}. Extensiones permitidas: {string.Join(", ", type.AllowedExtensions)}.");
    }
}