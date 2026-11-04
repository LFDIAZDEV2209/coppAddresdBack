using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Interfaces;

public interface IProgramAssignmentNoticeRepository
{
    /// <summary>Se ejecuta dentro de la transacción que crea la recomendación o activa el programa.</summary>
    Task AddProfessionalNoticesAsync(Guid recommendationId, Guid patientId, DateTime utcNow, CancellationToken ct);
    Task AddPatientNoticeAsync(Guid recommendationId, Guid patientId, DateTime utcNow, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListPendingAsync(Guid? recommendationId, DateTime utcNow, int limit, CancellationToken ct);
    Task<ProgramAssignmentNotice?> TryClaimAsync(Guid id, DateTime utcNow, CancellationToken ct);
    Task FinishAsync(Guid id, bool pushCompleted, bool chatCompleted, DateTime utcNow, CancellationToken ct);
    Task<AssignmentInboxPage> ListInboxAsync(Guid userId, int page, int pageSize, CancellationToken ct);
}
