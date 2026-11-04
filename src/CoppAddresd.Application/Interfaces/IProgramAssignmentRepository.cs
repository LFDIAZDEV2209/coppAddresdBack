using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Lectura de batería/perfil y persistencia atómica de la asignación post-tests.</summary>
public interface IProgramAssignmentRepository
{
    Task<Guid?> FindCompletedInitialBatteryAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct);
    Task<ProgramEnrollment?> FindPendingWithoutRecommendationAsync(Guid patientId, CancellationToken ct)
        => Task.FromResult<ProgramEnrollment?>(null);
    Task<bool> HasCurrentEnrollmentAsync(Guid patientId, CancellationToken ct);
    /// <summary>Serializa la creación por paciente y confirma todo o nada mediante estrategia de ejecución.</summary>
    Task ExecuteForPatientAsync(Guid patientId, Func<Task> action, CancellationToken ct);
    Task<AssignmentProfileData> ReadProfileAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct);
    Task<AssignmentCatalog> ReadCatalogAsync(CancellationToken ct);
    Task AddRecommendationAsync(ProgramAssignmentRecommendation recommendation, CancellationToken ct);
    Task<ProgramAssignmentRecommendation?> GetRecommendationAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListMissingNarrativesAsync(DateTime cutoff, int limit, CancellationToken ct);
    Task<bool> TryStartNarrativeAsync(Guid id, CancellationToken ct);
    Task<bool> TryStartNarrativeRegenerationAsync(Guid id, CancellationToken ct);
    Task SaveNarrativeAsync(Guid id, string narrativeEs, string narrativeEn, string? modelName, CancellationToken ct);
}
