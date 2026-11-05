using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Fallback interno del reconciliador; sin entrada HTTP ni decisiones manuales del ERP.</summary>
public sealed record ApplyAssignmentFallbackCommand(Guid RecommendationId) : IRequest<AppliedAssignmentDto?>;
public sealed class ApplyAssignmentFallbackCommandHandler(IProgramRepository programs, ICacheService cache,
    IProgramAssignmentNotifier notifier, TimeProvider clock, ILogger<ApplyAssignmentFallbackCommandHandler> logger)
    : IRequestHandler<ApplyAssignmentFallbackCommand, AppliedAssignmentDto?>
{
    public async Task<AppliedAssignmentDto?> Handle(ApplyAssignmentFallbackCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var now = clock.GetUtcNow().UtcDateTime;
        var result = await programs.ActivateAssignmentFallbackAsync(new(request.RecommendationId, now), ct);
        if (result is null || result.Replay) return result;
        await cache.RemoveAsync(CacheKeys.ScoresHistory(result.PatientId), ct);
        try { await notifier.DispatchAsync(result.RecommendationId, now, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Program.AssignmentNoticeDeferred RecommendationId={RecommendationId}", result.RecommendationId); }
        return result;
    }
}
