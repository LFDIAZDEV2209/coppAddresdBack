using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Conserva el documento pero lo excluye de las fuentes del agente. Reindexar permite restaurarlo.</summary>
public record ArchiveAgentDocumentCommand(Guid Id) : IRequest<AgentDocumentDto>;
public sealed class ArchiveAgentDocumentCommandHandler(IAgentCatalogRepository repository)
    : IRequestHandler<ArchiveAgentDocumentCommand, AgentDocumentDto>
{
    public async Task<AgentDocumentDto> Handle(ArchiveAgentDocumentCommand request, CancellationToken ct)
    {
        var document = await repository.GetDocumentAsync(request.Id, ct)
            ?? throw new NotFoundException("Documento no encontrado.");
        if (document.Status == AgentDocumentStatus.Procesando)
            throw new BusinessRuleViolationException("Espera a que termine la indexación antes de archivar.");
        document.Status = AgentDocumentStatus.Archivado;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await repository.TryArchiveDocumentAsync(document.Id, document.UpdatedAt.Value, ct))
            throw new BusinessRuleViolationException("La indexación comenzó mientras archivabas. Espera y vuelve a intentar.");
        return AgentDocumentDto.FromEntity(document);
    }
}
