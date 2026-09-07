using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Solicitud de intención de subida: el back genera la clave y la URL firmada.</summary>
/// <param name="Purpose">Propósito del objeto: <c>content</c> (default, audio/video) o <c>thumbnail</c> (imagen).</param>
public record CreateUploadIntentRequest(string FileName, string? ContentType, string? Purpose = null);

/// <summary>Respuesta del upload-intent con la clave y la URL a la que el front sube el archivo.</summary>
public record UploadIntentResponse(string StorageKey, string PresignedUrl, int ExpiresInSeconds);

/// <summary>Payload de creación de un medio desde el ERP.</summary>
public record CreateMediaItemRequest(
    string Title,
    string? Description,
    string Author,
    MediaType MediaType,
    MediaCategory Category,
    string StorageKey,
    string? ThumbnailKey,
    string? ContentType,
    long? FileSizeBytes,
    int? DurationSecs,
    MediaStatus Status,
    int SortOrder,
    int Day,
    int Month,
    IReadOnlyList<CoppAddresd.Domain.Entities.MediaChapterDto>? Chapters = null,
    IReadOnlyList<string>? Takeaways = null);

/// <summary>Payload de actualización de un medio desde el ERP.</summary>
public record UpdateMediaItemRequest(
    string Title,
    string? Description,
    string Author,
    MediaType MediaType,
    MediaCategory Category,
    string StorageKey,
    string? ThumbnailKey,
    string? ContentType,
    long? FileSizeBytes,
    int? DurationSecs,
    MediaStatus Status,
    int SortOrder,
    int Day,
    int Month,
    IReadOnlyList<CoppAddresd.Domain.Entities.MediaChapterDto>? Chapters = null,
    IReadOnlyList<string>? Takeaways = null);

/// <summary>Representación de un medio para la API.</summary>
public record MediaItemDto(
    Guid Id,
    string Title,
    string? Description,
    string Author,
    MediaType MediaType,
    MediaCategory Category,
    string StorageKey,
    string? ThumbnailKey,
    string? ContentType,
    long? FileSizeBytes,
    int? DurationSecs,
    MediaStatus Status,
    int SortOrder,
    int Day,
    int Month,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? CreatedBy,
    IReadOnlyList<CoppAddresd.Domain.Entities.MediaChapterDto> Chapters,
    IReadOnlyList<string> Takeaways)
{
    public static MediaItemDto FromEntity(CoppAddresd.Domain.Entities.MediaItem entity) => new(
        entity.Id,
        entity.Title,
        entity.Description,
        entity.Author,
        entity.MediaType,
        entity.Category,
        entity.StorageKey,
        entity.ThumbnailKey,
        entity.ContentType,
        entity.FileSizeBytes,
        entity.DurationSecs,
        entity.Status,
        entity.SortOrder,
        entity.Day,
        entity.Month,
        entity.PublishedAt,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.CreatedBy,
        entity.Chapters ?? [],
        entity.Takeaways ?? []);
}
