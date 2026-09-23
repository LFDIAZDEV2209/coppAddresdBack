using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Barrido de citas vencidas: cierra las citas <c>InProgress</c> cuyo fin
/// programado ya pasó, más la gracia efectiva de la ventana de sala de su
/// organización/clínica (si el paciente nunca ingresó queda <c>NoShow</c>; si
/// ingresó, <c>Completed</c>), y también las <c>Confirmed</c> que nunca
/// iniciaron sesión (nadie ingresó: <c>NoShow</c>). La sala del proveedor se
/// completa best-effort (un proveedor caído no bloquea el cierre). Lo ejecuta
/// <c>StaleSessionSweepHostedService</c> periódicamente.
/// </summary>
public sealed class StaleSessionSweeper(
    IAppointmentRepository appointments,
    IRoomRepository rooms,
    ITelemedicineSettingsProvider settingsProvider,
    IVideoProvider videoProvider,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineNotifier notifier,
    ILogger<StaleSessionSweeper> logger,
    ITelemedicineMetricsQueue? metricsQueue = null)
{
    /// <summary>Cierra las citas vencidas y devuelve cuántas cerró.</summary>
    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var inProgressClosed = await SweepInProgressAsync(now, ct);
        var neverStartedClosed = await SweepNeverStartedAsync(now, ct);
        return inProgressClosed + neverStartedClosed;
    }

    /// <summary>
    /// Cierra citas InProgress vencidas: NoShow si el paciente nunca ingresó,
    /// Completed si ingresó (el webhook de room-ended ya pudo haberla completado).
    /// </summary>
    private async Task<int> SweepInProgressAsync(DateTimeOffset now, CancellationToken ct)
    {
        var candidates = await appointments.ListByStatusEndingBeforeAsync(
            AppointmentStatus.InProgress,
            now,
            ct
        );

        var closed = 0;
        foreach (var candidate in candidates)
        {
            var settings = await settingsProvider.GetSettingsAsync(
                candidate.OrganizationId,
                candidate.ClinicId,
                ct
            );

            // Todavía dentro de la ventana de sala efectiva (considera reaperturas): no es estancada.
            var (_, roomClose) = SessionSupport.Window(candidate, settings);
            if (now < roomClose)
            {
                continue;
            }

            var appointment = await appointments.GetForUpdateAsync(candidate.Id, ct);
            if (appointment is null || appointment.Status != AppointmentStatus.InProgress)
            {
                continue; // otra instancia ya la cerró
            }

            var room = await rooms.GetForUpdateAsync(appointment.Id, ct);
            long? endedDuration = null;
            var sessionEndedNow = false;
            if (room is not null && room.Status != VirtualRoomStatus.Ended)
            {
                // ¿Había una sesión activa? EndActiveSession no devuelve la
                // sesión, así que se detecta antes para decidir el evento.
                sessionEndedNow = room.Sessions.Any(
                    s => s.Status == TelemedicineSessionStatus.Active);

                await CompleteProviderRoomAsync(room, ct);
                SessionSupport.EndActiveSession(room, endReason: "stale-sweep", endedBy: null, now);
                room.Status = VirtualRoomStatus.Ended;
                room.UpdatedAt = now.UtcDateTime;
                await rooms.UpdateAsync(room, ct);

                if (sessionEndedNow)
                {
                    endedDuration = room.Sessions
                        .Where(s => s.Status == TelemedicineSessionStatus.Ended)
                        .OrderByDescending(s => s.EndedAt)
                        .FirstOrDefault()
                        ?.DurationSeconds;
                }
            }

            var oldStatus = appointment.Status;

            // Sin ingreso del paciente la cita se cierra como NoShow; con ingreso,
            // como Completed (el webhook de room-ended ya pudo haberla completado).
            appointment.Status =
                room?.PatientJoinedAt is not null
                    ? AppointmentStatus.Completed
                    : AppointmentStatus.NoShow;
            if (appointment.Status == AppointmentStatus.Completed)
            {
                appointment.CompletedAt = now;
            }
            appointment.UpdatedAt = now.UtcDateTime;
            await appointments.UpdateAsync(appointment, ct);

            // F5 (deriva corregida): el cierre automático emite al pipeline, no
            // solo al backfill. InProgress → Completed/NoShow.
            if (metricsQueue is not null)
            {
                var scheduledDate = DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime);

                await metricsQueue.EnqueueAsync(new AppointmentStatusChangedMetricEvent(
                    appointment.Id,
                    appointment.ProfessionalId,
                    appointment.ClinicId,
                    scheduledDate,
                    oldStatus,
                    appointment.Status));

                if (sessionEndedNow)
                {
                    await metricsQueue.EnqueueAsync(new SessionEndedMetricEvent(
                        appointment.Id,
                        appointment.ProfessionalId,
                        appointment.ClinicId,
                        scheduledDate,
                        endedDuration));
                }
            }

            // F2: aviso informativo al paciente que no ingresó (push + SMS).
            if (appointment.Status == AppointmentStatus.NoShow)
            {
                await NotifyNoShowAsync(appointment, settings, ct);
            }

            logger.LogInformation(
                "Barrido: cita {AppointmentId} cerrada como {Status} (fin programado {ScheduledEnd:u}).",
                appointment.Id,
                appointment.Status,
                appointment.ScheduledEnd
            );
            closed++;
        }

        return closed;
    }

    /// <summary>
    /// Cierra citas Confirmed vencidas que nunca iniciaron sesión (nadie
    /// ingresó a la sala): quedan <c>NoShow</c>. Si existe sala, se completa
    /// best-effort en el proveedor.
    /// </summary>
    private async Task<int> SweepNeverStartedAsync(DateTimeOffset now, CancellationToken ct)
    {
        var candidates = await appointments.ListByStatusEndingBeforeAsync(
            AppointmentStatus.Confirmed,
            now,
            ct
        );

        var closed = 0;
        foreach (var candidate in candidates)
        {
            var settings = await settingsProvider.GetSettingsAsync(
                candidate.OrganizationId,
                candidate.ClinicId,
                ct
            );

            // Todavía dentro de la ventana de sala efectiva (considera reaperturas): no cerrar aún.
            var (_, roomClose) = SessionSupport.Window(candidate, settings);
            if (now < roomClose)
            {
                continue;
            }

            var appointment = await appointments.GetForUpdateAsync(candidate.Id, ct);
            if (appointment is null || appointment.Status != AppointmentStatus.Confirmed)
            {
                continue; // otra instancia ya la cerró
            }

            var room = await rooms.GetForUpdateAsync(appointment.Id, ct);
            if (room is not null && room.Sessions.Count > 0)
            {
                // Alguna sesión existió: no es una cita "nunca iniciada".
                continue;
            }

            if (room is not null && room.Status != VirtualRoomStatus.Ended)
            {
                await CompleteProviderRoomAsync(room, ct);
                room.Status = VirtualRoomStatus.Ended;
                room.UpdatedAt = now.UtcDateTime;
                await rooms.UpdateAsync(room, ct);
            }

            appointment.Status = AppointmentStatus.NoShow;
            appointment.UpdatedAt = now.UtcDateTime;
            await appointments.UpdateAsync(appointment, ct);

            // F5 (deriva corregida): Confirmed → NoShow también entra al pipeline.
            if (metricsQueue is not null)
            {
                await metricsQueue.EnqueueAsync(new AppointmentStatusChangedMetricEvent(
                    appointment.Id,
                    appointment.ProfessionalId,
                    appointment.ClinicId,
                    DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime),
                    AppointmentStatus.Confirmed,
                    AppointmentStatus.NoShow));
            }

            // F2: aviso informativo al paciente que no ingresó (push + SMS).
            await NotifyNoShowAsync(appointment, settings, ct);

            logger.LogInformation(
                "Barrido: cita {AppointmentId} nunca iniciada cerrada como NoShow (fin programado {ScheduledEnd:u}).",
                appointment.Id,
                appointment.ScheduledEnd
            );
            closed++;
        }

        return closed;
    }

    /// <summary>
    /// Avisa al paciente que no asistió a su cita (F2), best-effort: un fallo
    /// del backend de notificaciones nunca revierte ni detiene el cierre por
    /// NoShow. Se omite si las notificaciones están deshabilitadas o el paciente
    /// no tiene usuario de Auth.
    /// </summary>
    private async Task NotifyNoShowAsync(
        Appointment appointment,
        TelemedicineSettings settings,
        CancellationToken ct)
    {
        if (!settings.NotificationsEnabled)
        {
            return;
        }

        try
        {
            var patient = await referenceData.GetPatientAsync(appointment.PatientId, ct);
            if (patient?.UserId is not { } patientUserId)
            {
                logger.LogDebug(
                    "Paciente {PatientId} sin usuario de Auth: se omite la notificación de NoShow.",
                    appointment.PatientId
                );
                return;
            }

            await notifier.SendAsync(
                new TelemedicineNotification(
                    patientUserId,
                    "No asististe a tu cita",
                    $"No registramos tu ingreso a la cita de telemedicina del {appointment.ScheduledStart:g}. Puedes agendar una nueva cita.",
                    [TelemedicineNotificationChannel.Push, TelemedicineNotificationChannel.Sms],
                    NotificationSupport.Data(appointmentId: appointment.Id, screen: "room"),
                    $"appointment:{appointment.Id:N}:noshow"
                ),
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Barrido: no se pudo notificar el NoShow de la cita {AppointmentId}.",
                appointment.Id
            );
        }
    }

    private async Task CompleteProviderRoomAsync(VirtualRoom room, CancellationToken ct)
    {
        try
        {
            var providerRoom = await videoProvider.GetRoomAsync(room.ProviderRoomSid, ct);
            if (providerRoom is null)
            {
                return; // la sala ya no existe en el proveedor
            }

            await videoProvider.CompleteRoomAsync(room.ProviderRoomSid, ct);
        }
        catch (Exception ex)
        {
            // Best-effort: el proveedor nunca debe bloquear el cierre de la cita.
            logger.LogWarning(
                ex,
                "Barrido: no se pudo completar la sala {ProviderRoomSid} en el proveedor.",
                room.ProviderRoomSid
            );
        }
    }
}
