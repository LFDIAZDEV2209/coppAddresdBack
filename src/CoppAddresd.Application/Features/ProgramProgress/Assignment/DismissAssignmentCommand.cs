using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Desestima una recomendación visible al actor del JWT, sin activar ni modificar su inscripción.</summary>
public sealed record DismissAssignmentCommand(Guid RecommendationId, Guid ActorId,
    IReadOnlyList<Guid>? ScopedPatientIds) : IRequest<AssignmentDetailDto?>;

public sealed class DismissAssignmentCommandHandler(IProgramAssignmentOperationsRepository repository,
    ICacheService cache, TimeProvider clock) : IRequestHandler<DismissAssignmentCommand, AssignmentDetailDto?>
{
    public async Task<AssignmentDetailDto?> Handle(DismissAssignmentCommand request, CancellationToken ct)
    {
        if (request.ActorId == Guid.Empty)
            throw new FluentValidation.ValidationException("El actor autenticado es obligatorio.");
        var result = await repository.DismissAsync(request.RecommendationId, request.ActorId,
            request.ScopedPatientIds, clock.GetUtcNow().UtcDateTime, ct);
        if (result.Changed)
            await cache.SetAsync("program-assignment-revision:v1",
                new AssignmentCacheRevision(Guid.NewGuid().ToString("N")), TimeSpan.FromDays(1), ct);
        return result.Detail;
    }
}
