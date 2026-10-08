using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

public record HealthTestReminderDto(Guid PatientId, bool Sent, string Reason);
public record SendHealthTestReminderCommand(Guid PatientId) : IRequest<HealthTestReminderDto>;

/// <summary>Recordatorio real en el centro de avisos de la app. Máximo uno diario por paciente.</summary>
public sealed class SendHealthTestReminderCommandHandler(IHealthTestRepository tests, INotificationLogRepository notifications)
    : IRequestHandler<SendHealthTestReminderCommand, HealthTestReminderDto>
{
    public const string NotificationType = "health_test_reminder";
    public async Task<HealthTestReminderDto> Handle(SendHealthTestReminderCommand request, CancellationToken ct)
    {
        var assignments = await tests.ListActiveAssignmentsByPatientAsync(request.PatientId, ct);
        if (!assignments.Any(a => a.Status == HealthTestAssignmentStatus.pending || a.Status == HealthTestAssignmentStatus.in_progress))
            return new(request.PatientId, false, "No hay evaluaciones pendientes.");
        var now = DateTime.UtcNow;
        var sent = await notifications.TryAddDailyReminderAsync(new AppNotification
        {
            Id = Guid.NewGuid(), PatientId = request.PatientId, Type = NotificationType, Channel = "inapp",
            Title = "Evaluaciones de salud pendientes", Message = "Tu equipo de salud te recuerda completar las evaluaciones pendientes. Abre Tests de salud para continuar.",
            SentAt = now, Priority = "normal",
        }, ct);
        return new(request.PatientId, sent, sent ? "Recordatorio guardado en la app." : "El recordatorio de hoy ya fue enviado.");
    }
}

public record GetHealthTestRemindersQuery(Guid? ProfessionalId) : IRequest<IReadOnlyDictionary<Guid, int>>;
public sealed class GetHealthTestRemindersQueryHandler(IHealthTestRepository tests, INotificationLogRepository notifications)
    : IRequestHandler<GetHealthTestRemindersQuery, IReadOnlyDictionary<Guid, int>>
{
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(GetHealthTestRemindersQuery request, CancellationToken ct)
    {
        var ids = request.ProfessionalId is {} professionalId ? await tests.GetPatientIdsForProfessionalAsync(professionalId, ct) : null;
        return await notifications.CountByPatientAsync(SendHealthTestReminderCommandHandler.NotificationType, ids, ct);
    }
}
