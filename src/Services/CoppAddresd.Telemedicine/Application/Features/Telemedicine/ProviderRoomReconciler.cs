using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Recupera callbacks room-ended perdidos, incluso para citas iniciadas antes
/// de su fecha. Nunca desconecta una sala activa ni infiere un cierre por edad.
/// </summary>
public sealed class ProviderRoomReconciler(
    IRoomRepository rooms,
    IAppointmentRepository appointments,
    IVideoProvider video,
    ITelemedicineUnitOfWork unitOfWork,
    ILogger<ProviderRoomReconciler> logger,
    ITelemedicineMetricsQueue? metricsQueue = null)
{
    public async Task<int> ReconcileAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        Guid? cursor = null;
        var closed = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var batch = await rooms.ListActiveRoomsAsync(cursor, 100, ct);
            if (batch.Count == 0) return closed;
            foreach (var candidate in batch)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // I/O del proveedor fuera de la transacción de BD.
                    var providerRoom = await video.GetRoomAsync(candidate.ProviderRoomSid, ct);
                    if (providerRoom is null || providerRoom.Status != "completed"
                        || providerRoom.ProviderRoomSid != candidate.ProviderRoomSid)
                        continue;

                    AppointmentStatusChangedMetricEvent? statusEvent = null;
                    SessionEndedMetricEvent? sessionEvent = null;
                    var changed = false;
                    await unitOfWork.ExecuteInTransactionAsync(async token =>
                    {
                        changed = false;
                        statusEvent = null;
                        sessionEvent = null;
                        // Misma clave única que el webhook: dos instancias o un
                        // callback tardío no duplican estados ni métricas.
                        await rooms.AddWebhookEventAsync(new TelemedicineWebhookEvent
                        {
                            EventType = "room-ended",
                            RoomSid = candidate.ProviderRoomSid,
                            PayloadJson = JsonSerializer.Serialize(new
                            {
                                source = "provider-reconciliation", providerRoom.Status,
                                providerRoom.EndedAt
                            })
                        }, token);
                        var room = await rooms.GetForUpdateAsync(candidate.AppointmentId, token);
                        if (room is null || room.ProviderRoomSid != candidate.ProviderRoomSid) return;
                        var active = room.Sessions
                            .Where(s => s.Status == TelemedicineSessionStatus.Active)
                            .OrderByDescending(s => s.StartedAt).FirstOrDefault();
                        if (active is null) return;
                        var end = providerRoom.EndedAt ?? now;
                        if (end > now) end = now;
                        if (active.StartedAt is { } started && end < started) end = started;
                        SessionSupport.EndActiveSession(room, "provider-reconciliation", null, end);
                        room.Status = VirtualRoomStatus.Ended;
                        room.UpdatedAt = now.UtcDateTime;
                        var appointment = await appointments.GetForUpdateAsync(room.AppointmentId, token);
                        if (appointment is not null)
                        {
                            var day = DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime);
                            if (appointment.Status == AppointmentStatus.InProgress)
                            {
                                // Misma transición que room-ended; no se fabrica NoShow.
                                appointment.Status = AppointmentStatus.Completed;
                                appointment.CompletedAt = end;
                                appointment.UpdatedAt = now.UtcDateTime;
                                await appointments.UpdateAsync(appointment, token);
                                statusEvent = new(appointment.Id, appointment.ProfessionalId,
                                    appointment.ClinicId, day, AppointmentStatus.InProgress,
                                    AppointmentStatus.Completed);
                            }
                            sessionEvent = new(appointment.Id, appointment.ProfessionalId,
                                appointment.ClinicId, day, active.DurationSeconds);
                        }
                        await rooms.UpdateAsync(room, token);
                        changed = true;
                    }, ct);
                    if (changed) closed++;
                    if (metricsQueue is not null)
                    {
                        if (statusEvent is not null) await metricsQueue.EnqueueAsync(statusEvent);
                        if (sessionEvent is not null) await metricsQueue.EnqueueAsync(sessionEvent);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (BusinessRuleViolationException)
                {
                    // Otro conciliador/webhook ya reservó room-ended.
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "No se pudo conciliar la sala {RoomSid}.", candidate.ProviderRoomSid);
                }
            }
            cursor = batch[^1].Id;
        }
    }
}
