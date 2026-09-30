using CoppAddresd.Application.DTOs.Storage;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>
/// Recolector de huérfanos del storage (REQ-PCA-08, design D4): lista los
/// objetos bajo el prefijo <c>media/</c>, contrasta contra
/// <c>app.media_items</c> (StorageKey/ThumbnailKey) y — con
/// <paramref name="DryRun"/> = false — purga los huérfanos cuya última
/// modificación sea anterior a la ventana de retención
/// (<paramref name="RetentionDays"/>, default 7). La ventana protege los
/// uploads en curso: un archivo subido recientemente nunca se elimina antes
/// de que el usuario termine de registrar el medio.
/// </summary>
public record CleanupOrphanedBlobsCommand(bool DryRun = true, int RetentionDays = 7)
    : IRequest<CleanupOrphanedBlobsResult>;

/// <summary>Reporte de la corrida del recolector (contract REQ-PCA-08).</summary>
public sealed record CleanupOrphanedBlobsResult(
    bool DryRun,
    int RetentionDays,
    int OrphanedObjects,
    int PurgedObjects,
    long BytesFreed,
    IReadOnlyList<string> OrphanedKeys
);

/// <summary>Validación de input del recolector.</summary>
public sealed class CleanupOrphanedBlobsCommandValidator
    : AbstractValidator<CleanupOrphanedBlobsCommand>
{
    public CleanupOrphanedBlobsCommandValidator()
    {
        RuleFor(x => x.RetentionDays)
            .InclusiveBetween(1, 365)
            .WithMessage("La ventana de retención debe estar entre 1 y 365 días.");
    }
}

public sealed class CleanupOrphanedBlobsCommandHandler(
    IMediaItemRepository repository,
    IObjectStorageService objectStorage,
    ILogger<CleanupOrphanedBlobsCommandHandler> logger
) : IRequestHandler<CleanupOrphanedBlobsCommand, CleanupOrphanedBlobsResult>
{
    /// <summary>Límite de detalle de claves en la respuesta (el total nunca se recorta).</summary>
    private const int OrphanedKeysDetailLimit = 200;

    /// <summary>Prefijo de los objetos del catálogo de medios (convención upload-intent).</summary>
    private const string MediaPrefix = "media/";

    public async Task<CleanupOrphanedBlobsResult> Handle(
        CleanupOrphanedBlobsCommand request,
        CancellationToken ct
    )
    {
        var usedKeys = await repository.GetAllStorageKeysAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-request.RetentionDays);

        var orphaned = new List<ObjectMetadata>();
        string? continuationToken = null;
        do
        {
            var page = await objectStorage.ListObjectsAsync(MediaPrefix, continuationToken, ct);
            foreach (var obj in page.Items)
            {
                if (usedKeys.Contains(obj.Key))
                {
                    continue; // Referenciado por un MediaItem: nunca es huérfano.
                }

                if (obj.LastModified is null || obj.LastModified > cutoff)
                {
                    continue; // Ventana de retención: upload reciente, se preserva.
                }

                orphaned.Add(obj);
            }

            continuationToken = page.NextContinuationToken;
        } while (continuationToken is not null);

        var purged = 0;
        var bytesFreed = 0L;
        if (!request.DryRun && orphaned.Count > 0)
        {
            // Borrado en lote (semántica S3: tolera claves inexistentes).
            await objectStorage.DeleteObjectsAsync(orphaned.Select(o => o.Key), ct);
            purged = orphaned.Count;
            bytesFreed = orphaned.Sum(o => o.Size);
        }

        logger.LogInformation(
            "Media orphan cleanup: prefix={Prefix} retention={RetentionDays}d dryRun={DryRun} "
                + "orphaned={Orphaned} purged={Purged} bytesFreed={BytesFreed}",
            MediaPrefix,
            request.RetentionDays,
            request.DryRun,
            orphaned.Count,
            purged,
            bytesFreed
        );

        return new CleanupOrphanedBlobsResult(
            DryRun: request.DryRun,
            RetentionDays: request.RetentionDays,
            OrphanedObjects: orphaned.Count,
            PurgedObjects: purged,
            BytesFreed: bytesFreed,
            OrphanedKeys: orphaned.Take(OrphanedKeysDetailLimit).Select(o => o.Key).ToList()
        );
    }
}
