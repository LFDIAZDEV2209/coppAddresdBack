using CoppAddresd.Application.Features.ProgramProgress.Assignment;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Cola ERP, ficha acotada y candidatos del reconciliador, siempre paginados en PostgreSQL.</summary>
public interface IProgramAssignmentOperationsRepository
{
    Task<AssignmentQueuePage> ListAsync(string? status, int page, int pageSize, string? search,
        IReadOnlyList<Guid>? scopedPatientIds, DateTime utcNow, CancellationToken ct);
    /// <summary>Desestima solo estados pendientes; el replay conserva actor y fechas y no modifica la inscripción.</summary>
    Task<(AssignmentDetailDto? Detail, bool Changed)> DismissAsync(Guid id, Guid actorId,
        IReadOnlyList<Guid>? scopedPatientIds, DateTime utcNow, CancellationToken ct);
    Task<AssignmentDetailDto?> GetDetailAsync(Guid id, IReadOnlyList<Guid>? scopedPatientIds, CancellationToken ct);
    Task<IReadOnlyList<AssignmentRecoveryCandidate>> ListRecoveryAsync(DateTime utcNow, int maxAttempts, int limit, CancellationToken ct);
    Task<bool> TryClaimRecoveryAsync(Guid batteryAssignmentId, DateTime utcNow, int maxAttempts, int baseDelaySeconds, CancellationToken ct);
    Task<bool> HasRecommendationForPatientAsync(Guid patientId, CancellationToken ct);
    Task<IReadOnlyList<AssignmentFallbackCandidate>> ListFallbacksAsync(DateTime cutoff, int limit, CancellationToken ct);
}
