using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Cabecera publicada o histórica; excluye menús, notas y datos de otros pacientes.</summary>
public sealed record NutritionCycleHeader(Guid PrescriptionId, Guid CycleId, DateOnly StartDate,
    DateOnly EndDate, DateTime CreatedAt, bool IsPublished);

/// <summary>Persistencia del agregado de prescripción y lectura del catálogo semilla.</summary>
public interface INutritionPrescriptionRepository
{
    Task<IReadOnlyList<NutritionFoodItem>> ListFoodsAsync(CancellationToken ct);
    Task<NutritionPrescription?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<NutritionPrescription?> GetCurrentPublishedAsync(Guid patientId, DateOnly today, CancellationToken ct);
    Task<IReadOnlyList<NutritionCycleHeader>> ListCycleHeadersAsync(Guid patientId, CancellationToken ct);
    Task<IReadOnlyList<NutritionPrescription>> ListByPatientAsync(Guid patientId, int page, int pageSize, CancellationToken ct);
    Task AddAsync(NutritionPrescription prescription, CancellationToken ct);
    Task UpdateAsync(NutritionPrescription prescription, CancellationToken ct);
}
