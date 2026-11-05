using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Solicita regenerar ambos idiomas sin borrar la narrativa vigente ni modificar la decisión clínica.</summary>
public sealed record RegenerateNarrativeCommand(Guid RecommendationId, IReadOnlyList<Guid>? ScopedPatientIds)
    : IRequest<NarrativeQueuedDto?>;

public sealed record NarrativeQueuedDto(bool Queued);

public sealed class RegenerateNarrativeCommandHandler(IProgramAssignmentOperationsRepository repository,
    IProgramNarrativeQueue queue, ILogger<RegenerateNarrativeCommandHandler> logger)
    : IRequestHandler<RegenerateNarrativeCommand, NarrativeQueuedDto?>
{
    public async Task<NarrativeQueuedDto?> Handle(RegenerateNarrativeCommand request, CancellationToken ct)
    {
        var state = await repository.GetNarrativeStateAsync(request.RecommendationId, request.ScopedPatientIds, ct);
        if (state is null) return null;
        if (state == "dismissed")
            throw new BusinessRuleViolationException("ASSIGNMENT_DISMISSED: no se puede regenerar la narrativa de una recomendación desestimada.");
        ct.ThrowIfCancellationRequested();
        var queued = queue.TryEnqueue(request.RecommendationId, regenerate: true);
        if (!queued)
            logger.LogWarning("Program.NarrativeQueueFull RecommendationId={RecommendationId}", request.RecommendationId);
        return new(queued);
    }
}
