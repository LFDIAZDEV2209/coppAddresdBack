using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>
/// Elimina un medio (REQ-PCA-07, design D5). La guardia de integridad
/// referencial responde <see cref="DeleteMediaItemResult.Blocked"/> (→ 409
/// Conflict en la API) cuando el medio está referenciado en plantillas o en
/// snapshots de semanas de pacientes; en ese caso los blobs permanecen
/// intactos. Sin referencias: se elimina la fila y se invoca el borrado
/// físico de <c>StorageKey</c> y <c>ThumbnailKey</c> en el proveedor de
/// storage. Devuelve <see cref="DeleteMediaItemResult.NotFound"/> si el medio
/// no existe.
/// </summary>
public record DeleteMediaItemCommand(Guid Id) : IRequest<DeleteMediaItemResult>;

/// <summary>Resultado de la eliminación con el motivo (contrato REST de la API).</summary>
public sealed record DeleteMediaItemResult(
    bool NotFound,
    bool Blocked,
    MediaReferencesDto? BlockingReferences
);

public sealed class DeleteMediaItemCommandHandler(
    IMediaItemRepository repository,
    IObjectStorageService objectStorage,
    ILogger<DeleteMediaItemCommandHandler> logger
) : IRequestHandler<DeleteMediaItemCommand, DeleteMediaItemResult>
{
    public async Task<DeleteMediaItemResult> Handle(
        DeleteMediaItemCommand request,
        CancellationToken ct
    )
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
        {
            return new DeleteMediaItemResult(
                NotFound: true,
                Blocked: false,
                BlockingReferences: null
            );
        }

        // Guardia de integridad referencial (REQ-PCA-07): con referencias
        // activas la eliminación se bloquea (la API responde 409 Conflict y
        // el front sugiere archivar en vez de eliminar).
        var references = await repository.GetReferencesAsync(request.Id, ct);
        if (references is { HasReferences: true })
        {
            logger.LogInformation(
                "Media delete blocked by references: {Id} ({Title}) — {Total} referencias activas",
                entity.Id,
                entity.Title,
                references.TotalReferences
            );
            return new DeleteMediaItemResult(
                NotFound: false,
                Blocked: true,
                BlockingReferences: references
            );
        }

        // 1) Se elimina primero la fila de metadatos: si el borrado de blobs
        //    falla, el huérfano es recuperable por el recolector (GC) y la
        //    plataforma nunca queda con metadatos apuntando a blobs borrados
        //    (los pacientes verían "Contenido no disponible").
        await repository.DeleteAsync(entity, ct);

        // 2) Borrado físico de los objetos en storage (lote, tolera claves
        //    inexistentes). Un fallo del proveedor se degrada a warning: la
        //    fila ya no existe y el recolector de huérfanos purgará el resto.
        try
        {
            var keys = new List<string> { entity.StorageKey };
            if (!string.IsNullOrWhiteSpace(entity.ThumbnailKey))
            {
                keys.Add(entity.ThumbnailKey!);
            }

            await objectStorage.DeleteObjectsAsync(keys, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "No se pudo borrar el blob del medio {Id} (storageKey={StorageKey}): quedará como huérfano para el recolector",
                entity.Id,
                entity.StorageKey
            );
        }

        logger.LogInformation("MediaItem deleted: {Id} ({Title})", entity.Id, entity.Title);
        return new DeleteMediaItemResult(NotFound: false, Blocked: false, BlockingReferences: null);
    }
}
