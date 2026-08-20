using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>Detalle completo de un documento (una versión).</summary>
public record DocumentDto(
    Guid Id,
    Guid? PatientId,
    string? PatientName,
    Guid? ProfessionalId,
    Guid? ClinicId,
    string? ClinicName,
    Guid? LocationId,
    string? LocationName,
    Guid DocumentTypeId,
    string DocumentTypeName,
    Guid CategoryId,
    string CategoryName,
    string Title,
    string? Description,
    string StorageKey,
    string? ContentType,
    long? FileSizeBytes,
    int Version,
    Guid? ParentDocumentId,
    string Status,
    Guid? UploadedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int VersionsCount)
{
    public static DocumentDto FromEntity(Document entity, int versionsCount = 1) => new(
        entity.Id,
        entity.PatientId,
        entity.Patient is null ? null : $"{entity.Patient.FirstName} {entity.Patient.LastName}".Trim(),
        entity.ProfessionalId,
        entity.ClinicId,
        entity.Clinic?.Name,
        entity.LocationId,
        entity.Location?.Name,
        entity.DocumentTypeId,
        entity.DocumentType.Name,
        entity.DocumentType.CategoryId,
        entity.DocumentType.Category?.Name ?? string.Empty,
        entity.Title,
        entity.Description,
        entity.StorageKey,
        entity.ContentType,
        entity.FileSizeBytes,
        entity.Version,
        entity.ParentDocumentId,
        entity.Status,
        entity.UploadedBy,
        entity.CreatedAt,
        entity.UpdatedAt,
        versionsCount);
}

/// <summary>Fila del listado: última versión de un documento.</summary>
public record DocumentListItemDto(
    Guid Id,
    Guid? PatientId,
    string? PatientName,
    string Title,
    Guid DocumentTypeId,
    string DocumentTypeName,
    Guid CategoryId,
    string CategoryName,
    string Status,
    string? ContentType,
    long? FileSizeBytes,
    int Version,
    int VersionsCount,
    Guid? UploadedBy,
    DateTime CreatedAt)
{
    public static DocumentListItemDto FromEntity(Document entity, int versionsCount) => new(
        entity.Id,
        entity.PatientId,
        entity.Patient is null ? null : $"{entity.Patient.FirstName} {entity.Patient.LastName}".Trim(),
        entity.Title,
        entity.DocumentTypeId,
        entity.DocumentType.Name,
        entity.DocumentType.CategoryId,
        entity.DocumentType.Category?.Name ?? string.Empty,
        entity.Status,
        entity.ContentType,
        entity.FileSizeBytes,
        entity.Version,
        versionsCount,
        entity.UploadedBy,
        entity.CreatedAt);
}

/// <summary>Versión de un documento (historial).</summary>
public record DocumentVersionDto(
    Guid Id,
    int Version,
    string StorageKey,
    string? ContentType,
    long? FileSizeBytes,
    string Status,
    Guid? UploadedBy,
    DateTime CreatedAt)
{
    public static DocumentVersionDto FromEntity(Document entity) => new(
        entity.Id,
        entity.Version,
        entity.StorageKey,
        entity.ContentType,
        entity.FileSizeBytes,
        entity.Status,
        entity.UploadedBy,
        entity.CreatedAt);
}

/// <summary>Resultado paginado del listado de documentos.</summary>
public record PaginatedDocumentsResult(
    IReadOnlyList<DocumentListItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Categoría del catálogo de documentos.</summary>
public record DocumentCategoryDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder)
{
    public static DocumentCategoryDto FromEntity(DocumentCategory entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.SortOrder);
}

/// <summary>Tipo de documento del catálogo (con su categoría).</summary>
public record ClinicalDocumentTypeDto(
    Guid Id,
    Guid CategoryId,
    string Code,
    string Name,
    IReadOnlyList<string> AllowedExtensions,
    int SortOrder)
{
    public static ClinicalDocumentTypeDto FromEntity(ClinicalDocumentType entity) => new(
        entity.Id,
        entity.CategoryId,
        entity.Code,
        entity.Name,
        entity.AllowedExtensions,
        entity.SortOrder);
}

/// <summary>Catálogo completo de documentos: categorías con sus tipos.</summary>
public record DocumentCatalogDto(
    IReadOnlyList<DocumentCategoryDto> Categories,
    IReadOnlyList<ClinicalDocumentTypeDto> Types);

/// <summary>Resultado del intent de subida: clave de storage + expiración. La URL
/// firmada la construye el controlador (patrón MediaController, Request.Scheme/Host).</summary>
public record DocumentUploadIntentResult(
    string StorageKey,
    int ExpiresInSeconds);