using CoppAddresd.Domain.Entities.WhatsApp;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries;

public sealed record ProgramReminderReason(Guid PatientId, ReminderKind Kind, string SourceKey,
    string DetailEs, string DetailEn, Guid? AssignmentId = null);
public sealed record ProgramAccompanimentSnapshot(IReadOnlySet<Guid> ActivePatients,
    IReadOnlyList<ProgramReminderReason> Reasons);

/// <summary>Snapshot acotado del programa; el consumidor no accede a tablas clínicas.</summary>
public interface IProgramAccompanimentQuery
{
    Task<ProgramAccompanimentSnapshot> GetSnapshotAsync(IReadOnlyList<Guid> patientIds, DateTimeOffset now, CancellationToken ct);
    Task<Guid?> GetPatientIdAsync(Guid enrollmentId, CancellationToken ct);
}
