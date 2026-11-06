using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

public sealed record NutritionWorkspaceIdentity(Guid PatientId, string PatientName, string? Document, string Timezone);
public sealed record NutritionHistoryItem(Guid Id, Guid PrescriptionId, Guid CycleId, int Version, Guid ActorUserId,
    string ActorName, DateTime OccurredAt, string Action, int Revision, string? Comment, int CriticalCount, bool CriticalConfirmed, bool AutoApproved);
public sealed record NutritionHistoryPage(IReadOnlyList<NutritionHistoryItem> Items, int Total, int Page, int PageSize);

public interface INutritionWorkspaceRepository
{
    Task<NutritionWorkspaceIdentity?> IdentityAsync(Guid patientId, CancellationToken ct);
    Task<Guid?> LatestPrescriptionIdAsync(Guid patientId, CancellationToken ct);
    Task<NutritionDraftReservation?> GenerationAsync(Guid patientId, CancellationToken ct);
    Task<NutritionHistoryPage> HistoryAsync(Guid patientId, int page, int pageSize, CancellationToken ct);
}
