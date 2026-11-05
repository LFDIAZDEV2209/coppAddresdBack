using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Persistencia del agregado de prescripción y lectura del catálogo semilla.</summary>
public interface INutritionPrescriptionRepository
{
    Task<IReadOnlyList<NutritionFoodItem>> ListFoodsAsync(CancellationToken ct);
    Task<NutritionPrescription?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<NutritionPrescription>> ListByPatientAsync(Guid patientId, int page, int pageSize, CancellationToken ct);
    Task AddAsync(NutritionPrescription prescription, CancellationToken ct);
    Task UpdateAsync(NutritionPrescription prescription, CancellationToken ct);
}
