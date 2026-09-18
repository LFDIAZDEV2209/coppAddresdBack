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
    ILogger<StaleSessionSweeper> logger)
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

            // Todavía dentro de la ventana de sala (fin + gracia): no es estancada.
            if (now < candidate.ScheduledEnd.AddMinutes(settings.RoomCloseAfterMinutes))
            {
                continue;
            }

            var appointment = await appointments.GetForUpdateAsync(candidate.Id, ct);
            if (appointment is null || appointment.Status != AppointmentStatus.InProgress)
            {
                continue; // otra instancia ya la cerró
            }

            var room = await rooms.GetForUpdateAsync(appointment.Id, ct);
            if (room is not null && room.Status != VirtualRoomStatus.Ended)
            {
                await CompleteProviderRoomAsync(room, ct);
                SessionSupport.EndActiveSession(room, endReason: "stale-sweep", endedBy: null, now);
                room.Status = VirtualRoomStatus.Ended;
                room.UpdatedAt = now.UtcDateTime;
                await rooms.UpdateAsync(room, ct);
            }

            // Sin ingreso del paciente la cita se cierra como NoShow; con ingreso,
            // como Completed (el webhook de room-ended ya pudo haberla completado).
            appointment.Status =
                room?.PatientJoinedAt is not null
                    ? AppointmentStatus.Completed
                    : AppointmentStatus.NoShow;
            appointment.UpdatedAt = now.UtcDateTime;
            await appointments.UpdateAsync(appointment, ct);

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

            // Todavía dentro de la ventana de sala (fin + gracia): no cerrar aún.
            if (now < candidate.ScheduledEnd.AddMinutes(settings.RoomCloseAfterMinutes))
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

            logger.LogInformation(
                "Barrido: cita {AppointmentId} nunca iniciada cerrada como NoShow (fin programado {ScheduledEnd:u}).",
                appointment.Id,
                appointment.ScheduledEnd
            );
            closed++;
        }

        return closed;
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
