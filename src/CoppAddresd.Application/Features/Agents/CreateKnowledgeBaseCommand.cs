using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Crea una knowledge base global o ligada a un tipo de agente.</summary>
public record CreateKnowledgeBaseCommand(
    string Name,
    string? Description,
    string Scope,
    Guid? AgentTypeId,
    string? Status)
    : IRequest<KnowledgeBaseDto>;

public sealed class CreateKnowledgeBaseCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<CreateKnowledgeBaseCommandHandler> logger) : IRequestHandler<CreateKnowledgeBaseCommand, KnowledgeBaseDto>
{
    public async Task<KnowledgeBaseDto> Handle(CreateKnowledgeBaseCommand request, CancellationToken ct)
    {
        var scope = ParseScope(request.Scope);

        if (scope == KnowledgeBaseScope.Agent)
        {
            if (request.AgentTypeId is null)
                throw new InvalidOperationException("Una knowledge base de alcance Agent requiere AgentTypeId.");

            if (!await repository.AgentTypeExistsAsync(request.AgentTypeId.Value, ct))
                throw new InvalidOperationException($"El tipo de agente {request.AgentTypeId} no existe.");
        }

        var entity = new KnowledgeBase
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            Scope = scope,
            AgentTypeId = scope == KnowledgeBaseScope.Agent ? request.AgentTypeId : null,
            Status = ParseStatus(request.Status),
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddKnowledgeBaseAsync(entity, ct);

        logger.LogInformation("Knowledge base creada: {Id} ({Name})", entity.Id, entity.Name);

        var created = await repository.GetKnowledgeBaseAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la knowledge base creada.");

        return KnowledgeBaseDto.FromEntity(created);
    }

    internal static KnowledgeBaseScope ParseScope(string? scope)
    {
        if (!Enum.TryParse<KnowledgeBaseScope>(scope?.Trim(), ignoreCase: true, out var parsed))
            throw new InvalidOperationException($"Alcance de knowledge base inválido: '{scope}'.");

        return parsed;
    }

    internal static AgentStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return AgentStatus.Borrador;

        if (!Enum.TryParse<AgentStatus>(status.Trim(), ignoreCase: true, out var parsed))
            throw new InvalidOperationException($"Estado inválido: '{status}'.");

        return parsed;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}