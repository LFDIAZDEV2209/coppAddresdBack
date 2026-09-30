using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>
/// Transición semántica de publicación (REQ-PCA-02): valida de forma
/// sincrónica que el binario exista físicamente en storage y que los
/// metadatos registrados sean técnicamente conformes (MIME permitido,
/// tamaño &gt; 0 y dentro del umbral máximo, duración &gt; 0, portada existente
/// y de tipo imagen). Cualquier inconsistencia →
/// <see cref="UnprocessableEntityException"/> (422) y el medio permanece en
/// su estado previo. Idempotente: un medio ya Published conserva su
/// <c>PublishedAt</c> original.
/// </summary>
public record PublishMediaItemCommand(Guid Id) : IRequest<PublishMediaItemResult>;

/// <summary>Resultado de la publicación con el detalle de la validación técnica (contract §2).</summary>
public sealed record PublishMediaItemResult(
    Guid Id,
    MediaStatus Status,
    DateTimeOffset? PublishedAt,
    MediaPublishValidationDto Validation
);

/// <summary>Detalle de la verificación técnica del blob al publicar.</summary>
public sealed record MediaPublishValidationDto(
    bool StorageKeyVerified,
    long? FileSizeBytes,
    int? DurationSecs,
    bool ThumbnailVerified
);

/// <summary>
/// Reglas técnicas de publicación (design D3): MIME autorizados por tipo de
/// medio y tamaños máximos (250 MB audio, 1 GB video).
/// </summary>
public static class MediaPublishRules
{
    /// <summary>Content-Types de audio/podcast permitidos.</summary>
    public static readonly string[] AudioMimeTypes =
    [
        "audio/mpeg",
        "audio/mp4",
        "audio/aac",
        "audio/ogg",
        "audio/wav",
    ];

    /// <summary>Content-Types de video permitidos.</summary>
    public static readonly string[] VideoMimeTypes = ["video/mp4", "video/webm", "video/quicktime"];

    /// <summary>Tamaño máximo de un medio de audio: 250 MB.</summary>
    public const long MaxAudioBytes = 250L * 1024 * 1024;

    /// <summary>Tamaño máximo de un medio de video: 1 GB.</summary>
    public const long MaxVideoBytes = 1024L * 1024 * 1024;

    public static bool IsAudio(MediaType type) => type is MediaType.Podcast or MediaType.Audio;

    public static long MaxBytes(MediaType type) => IsAudio(type) ? MaxAudioBytes : MaxVideoBytes;

    public static bool IsAllowedContentType(MediaType type, string contentType) =>
        (IsAudio(type) ? AudioMimeTypes : VideoMimeTypes).Contains(
            contentType.Trim().ToLowerInvariant()
        );
}

public sealed class PublishMediaItemCommandHandler(
    IMediaItemRepository repository,
    IObjectStorageService objectStorage,
    ILogger<PublishMediaItemCommandHandler> logger
) : IRequestHandler<PublishMediaItemCommand, PublishMediaItemResult>
{
    public async Task<PublishMediaItemResult> Handle(
        PublishMediaItemCommand request,
        CancellationToken ct
    )
    {
        var entity =
            await repository.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Medio {request.Id} no encontrado.");

        // 1) El binario debe existir físicamente en storage (local o S3).
        var storageHead = await objectStorage.HeadObjectAsync(entity.StorageKey, ct);
        if (storageHead is null)
        {
            throw new UnprocessableEntityException(
                $"El archivo no existe en el storage en la clave '{entity.StorageKey}'. "
                    + "No se puede publicar un medio sin binario físico (el paciente recibiría 'Contenido no disponible')."
            );
        }

        // 2) Content-Type registrado y conforme al tipo de medio.
        var contentType = entity.ContentType?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(contentType))
        {
            throw new UnprocessableEntityException(
                "El Content-Type del archivo no está registrado; es obligatorio para publicar."
            );
        }

        if (!MediaPublishRules.IsAllowedContentType(entity.MediaType, contentType))
        {
            var allowed = MediaPublishRules.IsAudio(entity.MediaType)
                ? string.Join(", ", MediaPublishRules.AudioMimeTypes)
                : string.Join(", ", MediaPublishRules.VideoMimeTypes);
            throw new UnprocessableEntityException(
                $"El tipo MIME '{entity.ContentType}' no es compatible con un medio de tipo {entity.MediaType}. "
                    + $"Tipos permitidos: {allowed}."
            );
        }

        // 3) Tamaño registrado: mayor a 0 y dentro del umbral máximo.
        var fileSize = entity.FileSizeBytes;
        if (fileSize is null or <= 0)
        {
            throw new UnprocessableEntityException(
                "El tamaño del archivo debe ser mayor a 0 bytes."
            );
        }

        var maxBytes = MediaPublishRules.MaxBytes(entity.MediaType);
        if (fileSize > maxBytes)
        {
            throw new UnprocessableEntityException(
                $"El tamaño del archivo ({fileSize} bytes) excede el máximo permitido "
                    + $"({maxBytes} bytes) para un medio de tipo {entity.MediaType}."
            );
        }

        // 4) Duración: mayor a 0.
        if (entity.DurationSecs is null or <= 0)
        {
            throw new UnprocessableEntityException(
                "La duración en segundos debe ser mayor a 0 para poder publicar la lección."
            );
        }

        // 5) Portada (opcional): si está registrada, su blob debe existir y ser imagen.
        var thumbnailVerified = true;
        if (!string.IsNullOrWhiteSpace(entity.ThumbnailKey))
        {
            var thumbHead = await objectStorage.HeadObjectAsync(entity.ThumbnailKey!, ct);
            if (thumbHead is null)
            {
                throw new UnprocessableEntityException(
                    $"La imagen de portada no existe en el storage en la clave '{entity.ThumbnailKey}'."
                );
            }

            if (
                thumbHead.ContentType is not null
                && !thumbHead.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new UnprocessableEntityException(
                    $"La portada debe ser una imagen (Content-Type recibido: '{thumbHead.ContentType}')."
                );
            }

            thumbnailVerified = true;
        }

        // 6) Transición (idempotente): ya publicado conserva su PublishedAt original.
        if (entity.Status != MediaStatus.Published)
        {
            entity.Status = MediaStatus.Published;
            entity.PublishedAt = DateTimeOffset.UtcNow;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        // El UPDATE queda registrado en audit.activity_logs por el trigger
        // PostgreSQL (actor propagado vía AuditTriggerInterceptor/GUC).
        await repository.UpdateAsync(entity, ct);

        logger.LogInformation(
            "Media published: {Id} ({Title}) storageKey={StorageKey} size={FileSizeBytes} duration={DurationSecs}",
            entity.Id,
            entity.Title,
            entity.StorageKey,
            entity.FileSizeBytes,
            entity.DurationSecs
        );

        return new PublishMediaItemResult(
            entity.Id,
            entity.Status,
            entity.PublishedAt,
            new MediaPublishValidationDto(
                StorageKeyVerified: true,
                entity.FileSizeBytes,
                entity.DurationSecs,
                thumbnailVerified
            )
        );
    }
}
