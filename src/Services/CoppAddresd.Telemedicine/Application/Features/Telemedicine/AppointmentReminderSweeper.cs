using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Barrido de recordatorios de citas confirmadas (F2). Para cada cita
/// <c>Confirmed</c> que entra a una ventana se envía (best-effort, vía
/// <see cref="ITelemedicineNotifier"/>):
/// <list type="bullet">
/// <item>Primer recordatorio (default 24 h antes): push al paciente.</item>
/// <item>Segundo recordatorio (default 1 h antes): push al paciente (+ SMS si
/// entra a <c>SmsReminderHoursBefore</c>) y push al profesional.</item>
/// </list>
/// La deduplicación vive en <c>tele.notification_dispatch</c>: una fila única
/// <c>(appointment_id, kind)</c> por recordatorio. Si el backend rechaza el
/// envío no se registra (se reintenta el tick siguiente); si la cita dejó de
/// estar <c>Confirmed</c> (cancelada/completada/no-show/en curso) no se envía.
/// Lo ejecuta <c>AppointmentReminderSweepHostedService</c> periódicamente.
/// </summary>
public sealed class AppointmentReminderSweeper(
    IAppointmentRepository appointments,
    INotificationDispatchRepository dispatches,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    ITelemedicineNotifier notifier,
    ILogger<AppointmentReminderSweeper> logger
)
{
    /// <summary>
    /// Horizonte de consulta del barrido: se leen las citas confirmadas de las
    /// próximas 24 h (default de <c>ReminderFirstHoursBefore</c>) y cada fila
    /// decide su ventana efectiva con sus propios settings. Un valor mayor a
    /// 24 h se recorta a este horizonte (el recordatorio sale al entrar a la
    /// ventana de 24 h).
    /// </summary>
    private static readonly TimeSpan ReminderHorizon = TimeSpan.FromHours(24);

    /// <summary>
    /// Procesa las ventanas de recordatorio y devuelve cuántos despachos nuevos
    /// registró (un reenvío deduplicado no cuenta).
    /// </summary>
    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var candidates = await appointments.ListByStatusStartingBetweenAsync(
            AppointmentStatus.Confirmed,
            now,
            now.Add(ReminderHorizon),
            ct
        );

        var sent = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                sent += await ProcessCandidateAsync(candidate, now, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Fallo de una cita nunca detiene el barrido de las demás.
                logger.LogWarning(
                    ex,
                    "Falló el recordatorio de la cita {AppointmentId}.",
                    candidate.Id
                );
            }
        }

        return sent;
    }

    private async Task<int> ProcessCandidateAsync(
        Appointment candidate,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var settings = await settingsProvider.GetSettingsAsync(
            candidate.OrganizationId,
            candidate.ClinicId,
            ct
        );

        if (!settings.NotificationsEnabled)
        {
            return 0;
        }

        var lead = candidate.ScheduledStart - now;
        if (lead <= TimeSpan.Zero)
        {
            return 0; // ya comenzó: los recordatorios no aplican
        }

        var firstWindow = TimeSpan.FromHours(Math.Max(settings.ReminderFirstHoursBefore, 0));
        var secondWindow = TimeSpan.FromHours(Math.Max(settings.ReminderSecondHoursBefore, 0));

        var sent = 0;

        // Primer recordatorio (push): banda (segundo, primero] — una cita que ya
        // entró a la ventana corta no recibe además el aviso de 24 h.
        if (secondWindow < lead && lead <= firstWindow)
        {
            sent += await SendPatientReminderAsync(candidate, NotificationDispatchKind.Reminder24h, [TelemedicineNotificationChannel.Push], now, ct);
        }

        // Segundo recordatorio: banda (0, segundo].
        if (lead <= secondWindow)
        {
            IReadOnlyList<TelemedicineNotificationChannel> channels =
                lead <= TimeSpan.FromHours(Math.Max(settings.SmsReminderHoursBefore, 0))
                    ? [TelemedicineNotificationChannel.Push, TelemedicineNotificationChannel.Sms]
                    : [TelemedicineNotificationChannel.Push];

            sent += await SendPatientReminderAsync(candidate, NotificationDispatchKind.Reminder1h, channels, now, ct);
            sent += await SendProfessionalReminderAsync(candidate, now, ct);
        }

        return sent;
    }

    private async Task<int> SendPatientReminderAsync(
        Appointment appointment,
        NotificationDispatchKind kind,
        IReadOnlyList<TelemedicineNotificationChannel> channels,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var patient = await referenceData.GetPatientAsync(appointment.PatientId, ct);
        if (patient?.UserId is not { } patientUserId)
        {
            // Paciente sin cuenta de Auth: no hay destinatario posible.
            logger.LogDebug(
                "Paciente {PatientId} sin usuario de Auth: se omite el recordatorio {Kind}.",
                appointment.PatientId,
                kind
            );
            return 0;
        }

        var (title, body) = kind switch
        {
            NotificationDispatchKind.Reminder24h => (
                "Recordatorio de tu cita",
                $"Tu cita de telemedicina está programada para el {appointment.ScheduledStart:g}."
            ),
            _ => (
                "Tu cita de telemedicina es pronto",
                $"Tu cita comienza a las {appointment.ScheduledStart:g}. Ingresa a la sala virtual desde la app."
            ),
        };

        return await SendReminderAsync(appointment, kind, patientUserId, title, body, channels, now, ct);
    }

    private async Task<int> SendProfessionalReminderAsync(
        Appointment appointment,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var professional = await referenceData.GetProfessionalAsync(appointment.ProfessionalId, ct);
        if (professional?.UserId is not { } professionalUserId)
        {
            logger.LogDebug(
                "Profesional {ProfessionalId} sin usuario de Auth: se omite el recordatorio.",
                appointment.ProfessionalId
            );
            return 0;
        }

        return await SendReminderAsync(
            appointment,
            NotificationDispatchKind.ProfessionalReminder1h,
            professionalUserId,
            "Cita de telemedicina próxima",
            $"Tienes una cita de telemedicina a las {appointment.ScheduledStart:g}.",
            [TelemedicineNotificationChannel.Push],
            now,
            ct
        );
    }

    private async Task<int> SendReminderAsync(
        Appointment appointment,
        NotificationDispatchKind kind,
        Guid recipientUserId,
        string title,
        string body,
        IReadOnlyList<TelemedicineNotificationChannel> channels,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        if (await dispatches.ExistsAsync(appointment.Id, kind, ct))
        {
            return 0; // ya despachado: nunca se envía dos veces
        }

        // La cita pudo dejar de estar Confirmed entre la consulta y el envío.
        var current = await appointments.GetByIdAsync(appointment.Id, ct);
        if (current is null || current.Status != AppointmentStatus.Confirmed)
        {
            return 0;
        }

        var accepted = await notifier.SendAsync(
            new TelemedicineNotification(
                recipientUserId,
                title,
                body,
                channels,
                NotificationSupport.Data(appointmentId: appointment.Id, screen: "room"),
                $"appointment:{appointment.Id:N}:{DedupeSuffix(kind)}"
            ),
            ct
        );

        if (!accepted)
        {
            // Backend no disponible: no se registra → el próximo tick reintenta.
            logger.LogWarning(
                "El backend no aceptó el recordatorio {Kind} de la cita {AppointmentId}; se reintentará.",
                kind,
                appointment.Id
            );
            return 0;
        }

        var recorded = await dispatches.TryAddAsync(
            new NotificationDispatch
            {
                AppointmentId = appointment.Id,
                Kind = kind,
                SentAt = now.UtcDateTime,
            },
            ct
        );

        if (!recorded)
        {
            // Otra instancia ganó la carrera: el recordatorio ya salió una vez.
            logger.LogDebug(
                "El recordatorio {Kind} de la cita {AppointmentId} ya estaba registrado.",
                kind,
                appointment.Id
            );
            return 0;
        }

        return 1;
    }

    private static string DedupeSuffix(NotificationDispatchKind kind) =>
        kind switch
        {
            NotificationDispatchKind.Reminder24h => "reminder-24h",
            NotificationDispatchKind.Reminder1h => "reminder-1h",
            _ => "reminder-1h-professional",
        };
}
