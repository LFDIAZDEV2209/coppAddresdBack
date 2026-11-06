using CoppAddresd.Application.Features.Nutrition.Cases;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Proyección acotada de cabeceras; filtros y paginación se resuelven en la base de datos.</summary>
public interface INutritionInboxRepository
{
    Task<NutritionInboxData> ListAsync(NutritionActor actor, int page, int pageSize, string? state,
        string? search, DateTime utcNow, CancellationToken ct);
}

public sealed record NutritionInboxData(IReadOnlyList<NutritionInboxRow> Items, int Total);
public sealed record NutritionInboxRow(Guid PatientId, string PatientName, string State, Guid? PrescriptionId,
    int? Version, int? Revision, bool Provisional, double? K, DateOnly? StartDate, DateOnly? EndDate,
    bool IsPublished, string? ClinicalProfileJson, string? TargetsJson, string? GenerationState = null,
    bool GenerationFallback = false, string? GenerationReason = null, DateTime? GenerationUpdatedAt = null,
    bool HasPublishedToday = false, DateOnly? PublishedCycleStart = null, DateOnly? PublishedCycleEnd = null,
    string? LatestDraftState = null, bool HasFutureDraft = false);
