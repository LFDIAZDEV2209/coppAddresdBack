using CoppAddresd.Application.Features.Wellness;

namespace CoppAddresd.Application.Interfaces;

public sealed record NutritionReviewActor(Guid UserId, Guid? ProfessionalId, bool ViewAll);
public sealed record NutritionPlanApprovalInput(Guid Id, int Revision, string? Notes = null);
public sealed record NutritionPlanReviewEventDto(Guid Id, Guid? ActorUserId, DateTime OccurredAt,
    string Action, int Revision, string? Notes, string? ActorName = null);
public sealed record NutritionPlanStatusDto(string Status, int DurationDays = 30,
    DateOnly? StartDate = null, DateOnly? EndDate = null, int? CurrentDay = null,
    DateOnly? TodayLocalDate = null, Guid? PlanId = null, int? Revision = null);

public interface INutritionPlanWorkflow
{
    Task QueueIfReadyAsync(Guid patientId, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
    Task ReconcileAsync(CancellationToken ct);
    Task<NutritionPlanStatusDto> GetStatusAsync(Guid userId, string? timezone, CancellationToken ct);
    Task<bool> CanAccessPatientAsync(Guid patientId, NutritionReviewActor actor, CancellationToken ct);
    Task<IReadOnlyList<NutritionPlanDto>> ApproveAsync(IReadOnlyList<NutritionPlanApprovalInput> inputs,
        NutritionReviewActor actor, CancellationToken ct);
    Task<IReadOnlyList<NutritionPlanReviewEventDto>> HistoryAsync(Guid planId,
        NutritionReviewActor actor, CancellationToken ct);
}
