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
    Task<int> QueueUnpublishedReadyAsync(CancellationToken ct) => Task.FromResult(0);
    Task<NutritionDraftReservation> RequestAsync(Guid patientId, Guid batteryAssignmentId, DateTime now, CancellationToken ct);
    Task<NutritionDraftReservation> ReserveAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct);
    Task<NutritionDraftReservation?> ClaimAsync(DateTime now, CancellationToken ct);
    Task CompleteAsync(NutritionDraftReservation reservation, Guid prescriptionId, bool fallback, CancellationToken ct);
    Task FailAsync(NutritionDraftReservation reservation, string reason, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<(Guid PatientId, Guid BatteryId)>> MissingAsync(CancellationToken ct);
}
