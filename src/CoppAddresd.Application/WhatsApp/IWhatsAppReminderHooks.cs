namespace CoppAddresd.Application.WhatsApp;

/// <summary>Cancelación del canal después del commit clínico; opcional cuando está apagado.</summary>
public interface IWhatsAppReminderHooks
{
    Task CancelForAssignmentAsync(Guid assignmentId, CancellationToken ct);
    Task CancelForPatientAsync(Guid patientId, CancellationToken ct);
    Task RefreshEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task RefreshPatientAsync(Guid patientId, CancellationToken ct);
    Task RefreshPatientsAsync(IReadOnlyList<Guid> patientIds, CancellationToken ct);
}
