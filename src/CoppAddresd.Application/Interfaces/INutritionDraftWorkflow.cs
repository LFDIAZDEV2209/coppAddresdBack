using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Entrada y recuperación de recomendaciones; sin dependencias de la cola de asignación.</summary>
public interface INutritionDraftWorkflow
{
    Task<NutritionDraftReservation?> QueueAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
    Task ReconcileAsync(CancellationToken ct);
}

public interface INutritionDraftReservationStore
{
    Task<NutritionDraftReservation> ReserveAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct);
    Task<NutritionDraftReservation?> ClaimAsync(DateTime now, CancellationToken ct);
    Task CompleteAsync(NutritionDraftReservation reservation, Guid prescriptionId, bool fallback, CancellationToken ct);
    Task FailAsync(NutritionDraftReservation reservation, string reason, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<(Guid PatientId, Guid BatteryId)>> MissingAsync(CancellationToken ct);
}
