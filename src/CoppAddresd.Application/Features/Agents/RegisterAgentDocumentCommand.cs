using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Registra un documento en una knowledge base y dispara su indexación en el
/// AI Service (chunking + embeddings). El archivo ya debe estar subido a
/// storage (StorageController); el estado del documento refleja el resultado
/// del pipeline: <see cref="AgentDocumentStatus.Procesando"/> → Listo/Error.
/// </summary>
public record RegisterAgentDocumentCommand(
    Guid KnowledgeBaseId,
    string StorageKey,
    string FileName,
    string? ContentType,
    long? FileSizeBytes)
    : IRequest<AgentDocumentDto>;

public sealed class RegisterAgentDocumentCommandHandler(
    IAgentCatalogRepository repository,
    IObjectStorageService objectStorage,
    IAgentRuntimeSyncService runtimeSync,
    ILogger<RegisterAgentDocumentCommandHandler> logger)
    : IRequestHandler<RegisterAgentDocumentCommand, AgentDocumentDto>
{
    public async Task<AgentDocumentDto> Handle(RegisterAgentDocumentCommand request, CancellationToken ct)
    {
        var kb = await repository.GetKnowledgeBaseAsync(request.KnowledgeBaseId, ct)
            ?? throw new InvalidOperationException($"La knowledge base {request.KnowledgeBaseId} no existe.");

        var entity = new AgentDocument
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = kb.Id,
            StorageKey = request.StorageKey.Trim(),
            FileName = request.FileName.Trim(),
            ContentType = Normalize(request.ContentType),
            FileSizeBytes = request.FileSizeBytes,
            Status = AgentDocumentStatus.Procesando,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddDocumentAsync(entity, ct);

        logger.LogInformation("Documento registrado: {Id} ({FileName}) en KB {KnowledgeBaseId}",
            entity.Id, entity.FileName, entity.KnowledgeBaseId);

        await IndexAsync(entity, ct);

        return AgentDocumentDto.FromEntity(entity);
    }

    /// <summary>
    /// Lee el blob del storage y pide al AI Service que lo indexe; actualiza el
    /// estado del documento con el resultado (Listo + chunks / Error).
    /// </summary>
    private async Task IndexAsync(AgentDocument document, CancellationToken ct)
    {
        try
        {
            await using var stream = await objectStorage.GetObjectAsync(document.StorageKey, ct);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct);
            var bytes = memory.ToArray();

            var payload = new AgentDocumentIngestPayload(
                document.Id,
                document.KnowledgeBaseId,
                document.FileName,
                Convert.ToBase64String(bytes));

            var result = await runtimeSync.IngestDocumentAsync(payload, ct);

            document.Status = result.Status.Equals("indexado", StringComparison.OrdinalIgnoreCase)
                ? AgentDocumentStatus.Listo
                : AgentDocumentStatus.Error;
            document.ChunksCount = result.ChunksCreated;
            document.ErrorMessage = result.Error;
            document.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception exc) when (exc is FileNotFoundException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exc, "No se pudo indexar el documento {Id}", document.Id);
            document.Status = AgentDocumentStatus.Error;
            document.ErrorMessage = exc.Message;
            document.UpdatedAt = DateTime.UtcNow;
        }

        await repository.UpdateDocumentAsync(document, ct);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}