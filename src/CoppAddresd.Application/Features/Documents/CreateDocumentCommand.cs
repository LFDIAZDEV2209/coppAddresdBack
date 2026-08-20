using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>
/// Registra un documento (o una nueva versión de uno existente). El binario ya
/// fue subido al storage por el frontend vía upload-intent; aquí solo se
/// persiste la metadata. La clínica se hereda del paciente (regla anti-tenancy);
/// nunca se acepta del cuerpo. Las versiones son append-only: una fila hija
/// (ParentDocumentId) con Version incremental, heredando paciente/clínica de la raíz.
/// </summary>
public record CreateDocumentCommand(
    Guid? PatientId,
    Guid? ProfessionalId,
    Guid DocumentTypeId,
    string Title,
    string? Description,
    string StorageKey,
    string? ContentType,
    long? FileSizeBytes,
    Guid? ParentDocumentId,
    Guid? ClinicId,
    Guid? UploadedBy,
    Guid? CreatedBy)
    : IRequest<DocumentDto>;

public sealed class CreateDocumentCommandHandler(
    IDocumentRepository repository) : IRequestHandler<CreateDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(CreateDocumentCommand request, CancellationToken ct)
    {
        var type = await repository.GetTypeByIdAsync(request.DocumentTypeId, ct);
        if (type is null || !type.IsActive)
            throw new UnprocessableEntityException("El tipo de documento no existe o está inactivo.");

        ValidateExtension(type, request.StorageKey);

        if (!request.StorageKey.StartsWith("documents/", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleViolationException("La clave de storage debe vivir bajo documents/.");

        Guid? patientId = request.PatientId;
        Guid? clinicId = request.ClinicId;
        int version = 1;

        if (request.ParentDocumentId is { } parentId)
        {
            var root = await repository.GetRootAsync(parentId, ct);
            if (root is null)
                throw new NotFoundException("El documento raíz de la nueva versión no existe.");

            if (request.PatientId is { } givenPatient && givenPatient != root.PatientId)
                throw new BusinessRuleViolationException("La nueva versión debe pertenecer al mismo paciente que la versión raíz.");

            patientId = root.PatientId;
            clinicId = root.ClinicId;
            version = await repository.GetNextVersionAsync(parentId, ct);
        }
        else
        {
            if (request.PatientId is null)
                throw new UnprocessableEntityException("El documento requiere un paciente.");

            if (!await repository.PatientExistsAsync(request.PatientId.Value, ct))
                throw new NotFoundException("El paciente no existe.");

            // La clínica del documento es la del paciente; el cuerpo nunca la impone.
            clinicId = await repository.GetPatientClinicIdAsync(request.PatientId.Value, ct);
        }

        var now = DateTime.UtcNow;
        var document = new Document
        {
            PatientId = patientId,
            ProfessionalId = request.ProfessionalId,
            ClinicId = clinicId,
            DocumentTypeId = request.DocumentTypeId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StorageKey = request.StorageKey,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            Version = version,
            ParentDocumentId = request.ParentDocumentId,
            Status = "Ready",
            UploadedBy = request.UploadedBy,
            CreatedBy = request.CreatedBy,
            CreatedAt = now,
        };

        await repository.AddAsync(document, ct);

        var saved = await repository.GetByIdAsync(document.Id, ct);
        return DocumentDto.FromEntity(saved ?? document);
    }

    private static void ValidateExtension(Domain.Entities.ClinicalDocumentType type, string storageKey)
    {
        if (type.AllowedExtensions.Count == 0)
            return;

        var extension = Path.GetExtension(storageKey).TrimStart('.');
        if (string.IsNullOrWhiteSpace(extension))
            throw new BusinessRuleViolationException("La clave de storage debe incluir la extensión del archivo.");

        if (!type.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleViolationException(
                $"El tipo '{type.Name}' no admite archivos .{extension}. Extensiones permitidas: {string.Join(", ", type.AllowedExtensions)}.");
    }
}