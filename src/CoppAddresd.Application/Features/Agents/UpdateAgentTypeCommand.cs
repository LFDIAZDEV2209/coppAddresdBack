using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Actualiza los metadatos de un tipo de agente.</summary>
public record UpdateAgentTypeCommand(
    Guid Id,
    string? Name,
    string? Description,
    string? Specialty,
    string? IconKey,
    string? Slug,
    string? Status,
    string? Metadata)
    : IRequest<AgentTypeDto>;

public sealed class UpdateAgentTypeCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<UpdateAgentTypeCommandHandler> logger) : IRequestHandler<UpdateAgentTypeCommand, AgentTypeDto>
{
    public async Task<AgentTypeDto> Handle(UpdateAgentTypeCommand request, CancellationToken ct)
    {
        var entity = await repository.GetAgentTypeAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"El tipo de agente {request.Id} no existe.");

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            if (await repository.AgentTypeNameExistsAsync(name, entity.Id, ct))
                throw new InvalidOperationException($"Ya existe un tipo de agente llamado '{name}'.");

            entity.Name = name;
        }

        if (request.Description is not null) entity.Description = Normalize(request.Description);
        if (request.Specialty is not null) entity.Specialty = Normalize(request.Specialty);
        if (request.IconKey is not null) entity.IconKey = Normalize(request.IconKey);
        if (request.Slug is not null) entity.Slug = Normalize(request.Slug);
        if (request.Metadata is not null) entity.Metadata = request.Metadata;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AgentStatus>(request.Status.Trim(), ignoreCase: true, out var status))
                throw new InvalidOperationException($"Estado de agente inválido: '{request.Status}'.");

            entity.Status = status;
        }

        entity.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAgentTypeAsync(entity, ct);

        logger.LogInformation("Tipo de agente actualizado: {Id}", entity.Id);

        var updated = await repository.GetAgentTypeAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el tipo de agente actualizado.");

        return AgentTypeDto.FromEntity(updated);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}