using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Actualiza metadatos de una knowledge base.</summary>
public record UpdateKnowledgeBaseCommand(
    Guid Id,
    string? Name,
    string? Description,
    string? Status)
    : IRequest<KnowledgeBaseDto>;

public sealed class UpdateKnowledgeBaseCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<UpdateKnowledgeBaseCommandHandler> logger) : IRequestHandler<UpdateKnowledgeBaseCommand, KnowledgeBaseDto>
{
    public async Task<KnowledgeBaseDto> Handle(UpdateKnowledgeBaseCommand request, CancellationToken ct)
    {
        var entity = await repository.GetKnowledgeBaseAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"La knowledge base {request.Id} no existe.");

        if (!string.IsNullOrWhiteSpace(request.Name))
            entity.Name = request.Name.Trim();

        if (request.Description is not null)
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (!string.IsNullOrWhiteSpace(request.Status))
            entity.Status = CreateKnowledgeBaseCommandHandler.ParseStatus(request.Status);

        entity.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateKnowledgeBaseAsync(entity, ct);

        logger.LogInformation("Knowledge base actualizada: {Id}", entity.Id);

        var updated = await repository.GetKnowledgeBaseAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la knowledge base actualizada.");

        return KnowledgeBaseDto.FromEntity(updated);
    }
}