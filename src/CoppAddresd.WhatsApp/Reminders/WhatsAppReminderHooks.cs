using CoppAddresd.Application.WhatsApp;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.WhatsApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Reminders;

public sealed class WhatsAppReminderHooks(WhatsAppDbContext db, WhatsAppReminderScheduler scheduler,
    CoppAddresd.Application.Features.ProgramProgress.Queries.IProgramAccompanimentQuery programs) : IWhatsAppReminderHooks
{
    public async Task RefreshEnrollmentAsync(Guid enrollmentId, CancellationToken ct)
    {
        if (await programs.GetPatientIdAsync(enrollmentId, ct) is { } patientId)
            await RefreshPatientAsync(patientId, ct);
    }

    public Task RefreshPatientAsync(Guid patientId, CancellationToken ct) => RefreshPatientsAsync([patientId], ct);

    public async Task RefreshPatientsAsync(IReadOnlyList<Guid> patientIds, CancellationToken ct)
    {
        Guid? cursor = null;
        do { cursor = await scheduler.ScheduleBatchAsync(cursor, ct, patientIds); } while (cursor is not null);
    }

    public Task CancelForAssignmentAsync(Guid assignmentId, CancellationToken ct) => db.Reminders
        .Where(r => r.AssignmentId == assignmentId && r.State == ReminderState.Pending)
        .ExecuteUpdateAsync(set => set.SetProperty(r => r.State, ReminderState.Cancelled)
            .SetProperty(r => r.CancellationReason, "assessment_completed").SetProperty(r => r.LeaseUntil, (DateTimeOffset?)null), ct);

    public Task CancelForPatientAsync(Guid patientId, CancellationToken ct) => db.Reminders
        .Where(r => r.PatientId == patientId && r.State == ReminderState.Pending)
        .ExecuteUpdateAsync(set => set.SetProperty(r => r.State, ReminderState.Cancelled)
            .SetProperty(r => r.CancellationReason, "patient_interaction").SetProperty(r => r.LeaseUntil, (DateTimeOffset?)null), ct);
}
