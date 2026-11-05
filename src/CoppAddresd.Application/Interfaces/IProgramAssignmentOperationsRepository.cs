using CoppAddresd.Application.Features.ProgramProgress.Assignment;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Estado mínimo de narrativa y candidatos del reconciliador en PostgreSQL.</summary>
public interface IProgramAssignmentOperationsRepository
{
    Task<string?> GetNarrativeStateAsync(Guid id, IReadOnlyList<Guid>? scopedPatientIds, CancellationToken ct);
    Task<IReadOnlyList<AssignmentRecoveryCandidate>> ListRecoveryAsync(DateTime utcNow, int maxAttempts, int limit, CancellationToken ct);
    Task<bool> TryClaimRecoveryAsync(Guid batteryAssignmentId, DateTime utcNow, int maxAttempts, int baseDelaySeconds, CancellationToken ct);
    Task<bool> HasRecommendationForPatientAsync(Guid patientId, CancellationToken ct);
    Task<IReadOnlyList<AssignmentFallbackCandidate>> ListFallbacksAsync(DateTime cutoff, int limit, CancellationToken ct);
}
