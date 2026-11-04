using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>UseTemplateDefaults es interno al job; nunca se enlaza desde un body HTTP.</summary>
public sealed record ApplyAssignmentCommand(Guid RecommendationId, string? OverrideNutritionPlanName = null,
    string? OverrideRoutineName = null, bool Apply = true, Guid? ActorId = null,
    IReadOnlyList<Guid>? ScopedPatientIds = null, bool UseTemplateDefaults = false) : IRequest<AppliedAssignmentDto?>;
public sealed class ApplyAssignmentCommandHandler(IProgramRepository programs, ICacheService cache,
    IProgramAssignmentNotifier notifier, TimeProvider clock, ILogger<ApplyAssignmentCommandHandler> logger)
    : IRequestHandler<ApplyAssignmentCommand, AppliedAssignmentDto?>
{
    public async Task<AppliedAssignmentDto?> Handle(ApplyAssignmentCommand request, CancellationToken ct)
    {
        if (!request.Apply) throw new FluentValidation.ValidationException("apply debe ser true.");
        if (request.OverrideNutritionPlanName?.Length > 200 || request.OverrideRoutineName?.Length > 200)
            throw new FluentValidation.ValidationException("Los nombres admiten hasta 200 caracteres.");
        var now = clock.GetUtcNow().UtcDateTime;
        var result = await programs.ActivateAssignmentAsync(new(request.RecommendationId,
            request.OverrideNutritionPlanName, request.OverrideRoutineName, request.ActorId,
            request.ScopedPatientIds, now, request.UseTemplateDefaults), ct);
        if (result is null || result.Replay) return result;
        await cache.SetAsync("program-assignment-revision:v1", new AssignmentCacheRevision(Guid.NewGuid().ToString("N")), TimeSpan.FromDays(1), ct);
        await cache.RemoveAsync(CacheKeys.ScoresHistory(result.PatientId), ct);
        try { await notifier.DispatchAsync(result.RecommendationId, now, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Program.AssignmentNoticeDeferred RecommendationId={RecommendationId}", result.RecommendationId); }
        return result;
    }
}
