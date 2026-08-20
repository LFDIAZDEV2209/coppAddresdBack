using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>
/// Actualiza metadata de un documento (título, descripción, tipo, estado).
/// La metadata es mutable; las versiones del binario son append-only (nueva
/// versión = fila nueva). Devuelve <c>null</c> si el documento no existe.
/// </summary>
public record UpdateDocumentCommand(
    Guid Id,
    string? Title,
    string? Description,
    Guid? DocumentTypeId,
    string? Status,
    Guid? UpdatedBy)
    : IRequest<DocumentDto?>;

public sealed class UpdateDocumentCommandHandler(
    IDocumentRepository repository) : IRequestHandler<UpdateDocumentCommand, DocumentDto?>
{
    private static readonly string[] AllowedStatuses = ["Ready", "Archived"];

    public async Task<DocumentDto?> Handle(UpdateDocumentCommand request, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(request.Id, ct);
        if (document is null)
            return null;

        if (request.DocumentTypeId is { } newTypeId && newTypeId != document.DocumentTypeId)
        {
            var type = await repository.GetTypeByIdAsync(newTypeId, ct);
            if (type is null || !type.IsActive)
                throw new UnprocessableEntityException("El tipo de documento no existe o está inactivo.");

            ValidateExtension(type, document.StorageKey);
            document.DocumentTypeId = newTypeId;
        }

        if (request.Status is { } status)
        {
            if (!AllowedStatuses.Contains(status))
                throw new UnprocessableEntityException($"Estado inválido. Valores permitidos: {string.Join(", ", AllowedStatuses)}.");

            document.Status = status;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            document.Title = request.Title.Trim();

        if (request.Description is not null)
            document.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        document.UpdatedBy = request.UpdatedBy;
        document.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(document, ct);
        return DocumentDto.FromEntity(document);
    }

    private static void ValidateExtension(Domain.Entities.ClinicalDocumentType type, string storageKey)
    {
        if (type.AllowedExtensions.Count == 0)
            return;

        var extension = Path.GetExtension(storageKey).TrimStart('.');
        if (!type.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleViolationException(
                $"El tipo '{type.Name}' no admite archivos .{extension}. Extensiones permitidas: {string.Join(", ", type.AllowedExtensions)}.");
    }
}