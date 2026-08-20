using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Resultado del procesamiento de un webhook del proveedor.</summary>
public enum WebhookProcessOutcome
{
    Processed,
    Duplicate,
    InvalidSignature,
    UnknownEvent
}

/// <summary>Resultado del procesamiento de un webhook del proveedor.</summary>
public sealed record WebhookProcessResult(
    WebhookProcessOutcome Outcome,
    string? EventType);

/// <summary>
/// Procesa un webhook del proveedor de video (Twilio). Garantías:
/// <list type="bullet">
/// <item>Firma validada (X-Twilio-Signature) antes de procesar — 401 si es inválida.</item>
/// <item>Idempotencia: la clave única <c>(event_type, room_sid, participant_sid)</c>
/// del registro de eventos serializa los duplicados concurrentes (índice único en
/// BD); el perdedor recibe <see cref="WebhookProcessOutcome.Duplicate"/> y el
/// rollback de la transacción deshace sus mutaciones.</item>
/// <item>Atomicidad: reservar la clave + aplicar mutaciones en una sola transacción.</item>
/// </list>
/// </summary>
public sealed record ProcessTwilioWebhookCommand(
    string Url,
    string Signature,
    IReadOnlyDictionary<string, string> FormParams) : IRequest<WebhookProcessResult>;

public sealed class ProcessTwilioWebhookCommandHandler(
    IVideoProvider videoProvider,
    IRoomRepository rooms,
    IAppointmentRepository appointments,
    ITelemedicineUnitOfWork unitOfWork,
    ILogger<ProcessTwilioWebhookCommandHandler> logger)
    : IRequestHandler<ProcessTwilioWebhookCommand, WebhookProcessResult>
{
    public async Task<WebhookProcessResult> Handle(ProcessTwilioWebhookCommand request, CancellationToken ct)
    {
        var valid = await videoProvider.ValidateWebhookSignatureAsync(
            new WebhookValidationRequest(request.Url, request.Signature, request.FormParams), ct);

        if (!valid)
        {
            logger.LogWarning("Firma de webhook inválida para {Url}", request.Url);
            return new WebhookProcessResult(WebhookProcessOutcome.InvalidSignature, null);
        }

        var eventType = request.FormParams.GetValueOrDefault("EventType") ?? string.Empty;
        var roomSid = request.FormParams.GetValueOrDefault("RoomSid") ?? string.Empty;
        var participantSid = request.FormParams.GetValueOrDefault("ParticipantSid") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(roomSid))
        {
            logger.LogDebug("Webhook sin EventType/RoomSid ignorado.");
            return new WebhookProcessResult(WebhookProcessOutcome.UnknownEvent, eventType);
        }

        var payloadJson = JsonSerializer.Serialize(request.FormParams);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async txCt =>
            {
                // 1. Reservar la clave de idempotencia (insert). Si ya existe
                //    (duplicado), el índice único lanza y el rollback deshace todo.
                await rooms.AddWebhookEventAsync(new TelemedicineWebhookEvent
                {
                    EventType = eventType,
                    RoomSid = roomSid,
                    ParticipantSid = participantSid,
                    PayloadJson = payloadJson,
                }, txCt);

                // 2. Aplicar las mutaciones según el tipo de evento.
                await ApplyEventAsync(eventType, roomSid, now: DateTimeOffset.UtcNow, txCt);
            }, ct);
        }
        catch (BusinessRuleViolationException)
        {
            return new WebhookProcessResult(WebhookProcessOutcome.Duplicate, eventType);
        }

        return new WebhookProcessResult(WebhookProcessOutcome.Processed, eventType);
    }

    private async Task ApplyEventAsync(string eventType, string roomSid, DateTimeOffset now, CancellationToken ct)
    {
        var room = await rooms.GetForUpdateByProviderRoomSidAsync(roomSid, ct);
        if (room is null)
        {
            // Sala desconocida (evento anterior a nuestra fila o sala ajena): se
            // registra el webhook (auditoría) pero no se aplican mutaciones.
            logger.LogDebug("Webhook {EventType} de sala desconocida {RoomSid} registrado sin mutaciones.",
                eventType, roomSid);
            return;
        }

        switch (eventType)
        {
            case "room-ended":
                room.Status = VirtualRoomStatus.Ended;
                room.UpdatedAt = now.UtcDateTime;
                EndActiveSession(room, endReason: "room-ended", endedBy: null, now);
                await CompleteAppointmentIfInProgressAsync(room.AppointmentId, now, ct);
                break;

            case "participant-connected":
                if (room.Status is VirtualRoomStatus.Created or VirtualRoomStatus.Waiting)
                {
                    room.Status = VirtualRoomStatus.Active;
                    room.UpdatedAt = now.UtcDateTime;
                }
                TouchActiveSession(room, now);
                break;

            case "participant-disconnected":
                TouchActiveSession(room, now);
                break;

            default:
                // Otros eventos (room-created, recordings, etc.): solo se registran.
                logger.LogDebug("Webhook {EventType} sin efecto de dominio.", eventType);
                break;
        }

        await rooms.UpdateAsync(room, ct);
    }

    private static void EndActiveSession(VirtualRoom room, string endReason, Guid? endedBy, DateTimeOffset now)
    {
        var active = room.Sessions
            .Where(s => s.Status == TelemedicineSessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (active is null)
        {
            return;
        }

        active.Status = TelemedicineSessionStatus.Ended;
        active.EndedAt = now;
        active.DurationSeconds = active.StartedAt is { } startedAt
            ? (long)Math.Max(0, (now - startedAt).TotalSeconds)
            : null;
        active.EndedBy = endedBy;
        active.EndReason = endReason;
    }

    private static void TouchActiveSession(VirtualRoom room, DateTimeOffset now)
    {
        var active = room.Sessions
            .Where(s => s.Status == TelemedicineSessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (active is not null)
        {
            active.LastProviderEventAt = now.UtcDateTime;
        }
    }

    private async Task CompleteAppointmentIfInProgressAsync(Guid appointmentId, DateTimeOffset now, CancellationToken ct)
    {
        // La sala terminó: si la cita estaba en curso, pasa a completada. Si nunca
        // se inició sesión (nadie entró), se deja como está (NoShow es una
        // decisión de negocio aparte, no una conclusión automática de la sala).
        var appointment = await appointments.GetForUpdateAsync(appointmentId, ct);
        if (appointment is null || appointment.Status != AppointmentStatus.InProgress)
        {
            return;
        }

        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedAt = now.UtcDateTime;
        await appointments.UpdateAsync(appointment, ct);
    }
}
