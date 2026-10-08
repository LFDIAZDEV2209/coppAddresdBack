using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Pipeline compartido de registro/reintento: conserva el documento y sus IDs.</summary>
public sealed class AgentDocumentIndexer(
    IAgentCatalogRepository repository,
    IObjectStorageService objectStorage,
    IAgentRuntimeSyncService runtimeSync,
    ILogger<AgentDocumentIndexer> logger)
{
    // La reserva vence después del límite del trabajo; un proceso muerto es recuperable.
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan IndexTimeout = TimeSpan.FromMinutes(10);

    public async Task IndexAsync(AgentDocument document, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // PostgreSQL guarda microsegundos; truncar evita perder la comparación de reserva.
        var ticks = DateTimeOffset.UtcNow.Ticks;
        var startedAt = new DateTimeOffset(ticks - ticks % 10, TimeSpan.Zero);
        if (!await repository.TryStartDocumentIndexAsync(
            document.Id, startedAt, startedAt - LeaseDuration, ct))
            throw new CoppAddresd.Domain.Exceptions.BusinessRuleViolationException(
                "El documento ya se está indexando. Espera unos minutos antes de reintentar.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(IndexTimeout);
        try
        {
            await using var stream = await objectStorage.GetObjectAsync(document.StorageKey, timeout.Token);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, timeout.Token);
            var result = await runtimeSync.IngestDocumentAsync(new AgentDocumentIngestPayload(
                document.Id, document.KnowledgeBaseId, document.FileName,
                Convert.ToBase64String(memory.ToArray())), timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            document.Status = string.Equals(result.Status, "indexado", StringComparison.OrdinalIgnoreCase)
                && result.ChunksCreated > 0 && string.IsNullOrWhiteSpace(result.Error)
                ? AgentDocumentStatus.Listo : AgentDocumentStatus.Error;
            document.ChunksCount = result.ChunksCreated;
            document.ErrorMessage = document.Status == AgentDocumentStatus.Listo ? null
                : "No se pudo indexar el documento. Reintenta la indexación.";
            if (document.Status == AgentDocumentStatus.Error)
                logger.LogWarning("Indexación fallida de {DocumentId}: {Status}, {Error}",
                    document.Id, result.Status, result.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            document.Status = AgentDocumentStatus.Error;
            document.ChunksCount = null;
            document.ErrorMessage = "La indexación fue interrumpida. Puedes reintentar.";
            await PersistAsync(document, startedAt);
            throw;
        }
        catch (Exception exc)
        {
            // El error queda explícito y recuperable; no se exponen rutas ni detalles internos.
            logger.LogWarning(exc, "No se pudo indexar el documento {DocumentId}", document.Id);
            document.Status = AgentDocumentStatus.Error;
            document.ChunksCount = null;
            document.ErrorMessage = "No se pudo indexar el documento. Reintenta la indexación.";
        }

        await PersistAsync(document, startedAt);
    }

    private async Task PersistAsync(AgentDocument document, DateTimeOffset startedAt)
    {
        document.UpdatedAt = DateTimeOffset.UtcNow;
        // Limpieza crítica: el request puede haberse cancelado tras guardar Procesando.
        // Token propio limitado para no abandonar ese estado ni retener recursos sin límite.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        if (!await repository.CompleteDocumentIndexAsync(document, startedAt, cleanup.Token))
            throw new CoppAddresd.Domain.Exceptions.BusinessRuleViolationException(
                "El documento cambió durante la indexación. Vuelve a cargar la lista.");
    }
}
