using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Reindexa el blob existente sin crear otro documento ni duplicar sus chunks.</summary>
public record RetryAgentDocumentCommand(Guid Id) : IRequest<AgentDocumentDto>;

public sealed class RetryAgentDocumentCommandHandler(
    IAgentCatalogRepository repository, AgentDocumentIndexer indexer)
    : IRequestHandler<RetryAgentDocumentCommand, AgentDocumentDto>
{
    public async Task<AgentDocumentDto> Handle(RetryAgentDocumentCommand request, CancellationToken ct)
    {
        var document = await repository.GetDocumentAsync(request.Id, ct)
            ?? throw new NotFoundException("Documento no encontrado.");
        await indexer.IndexAsync(document, ct);
        return AgentDocumentDto.FromEntity(document);
    }
}
