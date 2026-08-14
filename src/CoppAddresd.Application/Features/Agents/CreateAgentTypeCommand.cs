using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Crea un tipo de agente en estado Borrador.</summary>
public record CreateAgentTypeCommand(
    string Name,
    string? Description,
    string? Specialty,
    string? IconKey,
    string? Metadata)
    : IRequest<AgentTypeDto>;

public sealed class CreateAgentTypeCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<CreateAgentTypeCommandHandler> logger) : IRequestHandler<CreateAgentTypeCommand, AgentTypeDto>
{
    public async Task<AgentTypeDto> Handle(CreateAgentTypeCommand request, CancellationToken ct)
    {
        if (await repository.AgentTypeNameExistsAsync(request.Name.Trim(), null, ct))
        {
            throw new InvalidOperationException($"Ya existe un tipo de agente llamado '{request.Name.Trim()}'.");
        }

        var entity = new AgentType
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            Specialty = Normalize(request.Specialty),
            IconKey = Normalize(request.IconKey),
            Status = AgentStatus.Borrador,
            Metadata = string.IsNullOrWhiteSpace(request.Metadata) ? "{}" : request.Metadata,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddAgentTypeAsync(entity, ct);

        logger.LogInformation("Tipo de agente creado: {Id} ({Name})", entity.Id, entity.Name);

        var created = await repository.GetAgentTypeAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el tipo de agente creado.");

        return AgentTypeDto.FromEntity(created);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}