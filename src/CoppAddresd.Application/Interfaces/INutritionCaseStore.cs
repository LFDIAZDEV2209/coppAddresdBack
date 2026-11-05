using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

public interface INutritionCaseStore
{
    Task<NutritionCaseContext?> GetContextAsync(Guid patientId, CancellationToken ct);
    Task<bool> CanAccessAsync(Guid patientId, NutritionActor actor, CancellationToken ct);
    Task<T> ExecuteAsync<T>(Guid patientId, Func<CancellationToken, Task<T>> operation, CancellationToken ct);
    Task<int> NextVersionAsync(Guid cycleId, CancellationToken ct);
    Task<IReadOnlyList<NutritionPrescription>> PublishedAsync(Guid patientId, CancellationToken ct);
    Task AddEventAsync(NutritionPrescriptionEvent entry, CancellationToken ct);
    Task<IReadOnlyList<NutritionPrescriptionEvent>> HistoryAsync(Guid id, CancellationToken ct);
}
public interface INutritionDraftClient
{
    Task<NutritionDraftResponse> GenerateAsync(NutritionDraftRequest request, CancellationToken ct);
}
