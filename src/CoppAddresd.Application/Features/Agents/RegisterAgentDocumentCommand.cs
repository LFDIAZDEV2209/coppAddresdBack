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
    AgentDocumentIndexer indexer,
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
            Status = AgentDocumentStatus.Pendiente,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddDocumentAsync(entity, ct);

        logger.LogInformation("Documento registrado: {Id} ({FileName}) en KB {KnowledgeBaseId}",
            entity.Id, entity.FileName, entity.KnowledgeBaseId);

        await indexer.IndexAsync(entity, ct);

        return AgentDocumentDto.FromEntity(entity);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}