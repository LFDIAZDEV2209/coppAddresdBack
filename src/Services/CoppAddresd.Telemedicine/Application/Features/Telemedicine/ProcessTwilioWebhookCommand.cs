using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
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
    IAppointmentReferenceDataService referenceData,
    IAlertRepository alerts,
    ITelemedicineUnitOfWork unitOfWork,
    ILogger<ProcessTwilioWebhookCommandHandler> logger,
    ITelemedicineMetricsQueue? metricsQueue = null)
    : IRequestHandler<ProcessTwilioWebhookCommand, WebhookProcessResult>
{
    /// <summary>
    /// Eventos de métricas producidos por un webhook procesado. Se emiten
    /// FUERA de la transacción (después del commit): un duplicado que hace
    /// rollback no debe contar dos veces (F5).
    /// </summary>
    private sealed record WebhookMetricEvents(
        AppointmentStatusChangedMetricEvent? StatusChanged,
        SessionEndedMetricEvent? SessionEnded)
    {
        public static readonly WebhookMetricEvents None = new(null, null);
    }

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
        // Identity del token Twilio = usuario del JWT (join-token); identifica al
        // participante para las alertas de la bandeja (profesional vs paciente).
        var participantIdentity = request.FormParams.GetValueOrDefault("Identity") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(roomSid))
        {
            logger.LogDebug("Webhook sin EventType/RoomSid ignorado.");
            return new WebhookProcessResult(WebhookProcessOutcome.UnknownEvent, eventType);
        }

        var payloadJson = JsonSerializer.Serialize(request.FormParams);
        var metricEvents = WebhookMetricEvents.None;

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
                metricEvents = await ApplyEventAsync(eventType, roomSid, participantIdentity, now: DateTimeOffset.UtcNow, txCt);
            }, ct);
        }
        catch (BusinessRuleViolationException)
        {
            return new WebhookProcessResult(WebhookProcessOutcome.Duplicate, eventType);
        }

        // 3. Métricas (F5) después del commit: el duplicado que hace rollback
        //    nunca llega aquí, y la cola es en memoria (sin impacto en la request).
        if (metricsQueue is not null)
        {
            if (metricEvents.StatusChanged is not null)
            {
                await metricsQueue.EnqueueAsync(metricEvents.StatusChanged);
            }

            if (metricEvents.SessionEnded is not null)
            {
                await metricsQueue.EnqueueAsync(metricEvents.SessionEnded);
            }
        }

        return new WebhookProcessResult(WebhookProcessOutcome.Processed, eventType);
    }

    private async Task<WebhookMetricEvents> ApplyEventAsync(string eventType, string roomSid, string participantIdentity, DateTimeOffset now, CancellationToken ct)
    {
        var room = await rooms.GetForUpdateByProviderRoomSidAsync(roomSid, ct);
        if (room is null)
        {
            // Sala desconocida (evento anterior a nuestra fila o sala ajena): se
            // registra el webhook (auditoría) pero no se aplican mutaciones.
            logger.LogDebug("Webhook {EventType} de sala desconocida {RoomSid} registrado sin mutaciones.",
                eventType, roomSid);
            return WebhookMetricEvents.None;
        }

        var metricEvents = WebhookMetricEvents.None;

        switch (eventType)
        {
            case "room-ended":
                room.Status = VirtualRoomStatus.Ended;
                room.UpdatedAt = now.UtcDateTime;

                // La sesión activa (si existe) se captura antes de cerrarla para
                // reportar su duración en las métricas de llamada.
                var activeSession = room.Sessions
                    .Where(s => s.Status == TelemedicineSessionStatus.Active)
                    .OrderByDescending(s => s.StartedAt)
                    .FirstOrDefault();

                SessionSupport.EndActiveSession(room, endReason: "room-ended", endedBy: null, now);

                metricEvents = await CompleteAppointmentIfInProgressAsync(
                    room.AppointmentId, activeSession is not null, activeSession?.DurationSeconds, now, ct);
                await EmitSessionAlertsAsync(room.AppointmentId, eventType, participantIdentity, ct);
                break;

            case "participant-connected":
                if (room.Status is VirtualRoomStatus.Created or VirtualRoomStatus.Waiting)
                {
                    room.Status = VirtualRoomStatus.Active;
                    room.UpdatedAt = now.UtcDateTime;
                }
                TouchActiveSession(room, now);
                await TrackPatientJoinAsync(room, participantIdentity, now, ct);
                await EmitSessionAlertsAsync(room.AppointmentId, eventType, participantIdentity, ct);
                break;

            case "participant-disconnected":
                TouchActiveSession(room, now);
                await EmitSessionAlertsAsync(room.AppointmentId, eventType, participantIdentity, ct);
                break;

            default:
                // Otros eventos (room-created, recordings, etc.): solo se registran.
                logger.LogDebug("Webhook {EventType} sin efecto de dominio.", eventType);
                break;
        }

        await rooms.UpdateAsync(room, ct);
        return metricEvents;
    }

    /// <summary>
    /// Materializa las alertas de la bandeja según el evento del proveedor. El
    /// participante se identifica por el <c>Identity</c> del token Twilio (que
    /// es el usuario del JWT): si coincide con el profesional de la cita, la
    /// alerta avisa al profesional (su identidad) — la alerta al paciente queda
    /// para una fase futura (app móvil). Best-effort: un fallo aquí no debe
    /// romper el procesamiento del webhook.
    /// </summary>
    private async Task EmitSessionAlertsAsync(
        Guid appointmentId,
        string eventType,
        string participantIdentity,
        CancellationToken ct)
    {
        try
        {
            var appointment = await appointments.GetForUpdateAsync(appointmentId, ct);
            if (appointment is null)
            {
                return;
            }

            var professional = await referenceData.GetProfessionalAsync(appointment.ProfessionalId, ct);

            // Solo el profesional (vía su identidad de usuario) recibe estas
            // alertas en esta fase.
            var recipientUserId = professional?.UserId;
            if (recipientUserId is null)
            {
                return;
            }

            var patientName = (await referenceData.GetPatientAsync(appointment.PatientId, ct))?.FullName
                              ?? "el paciente";

            var isProfessionalParticipant = Guid.TryParse(participantIdentity, out var participantUserId)
                                            && participantUserId == recipientUserId;

            TelemedicineAlert? alert = eventType switch
            {
                "participant-connected" => isProfessionalParticipant
                    ? AlertMaterializer.ProfessionalJoined(recipientUserId, appointmentId, patientName)
                    : AlertMaterializer.PatientWaiting(recipientUserId, appointmentId, patientName),
                "participant-disconnected" => isProfessionalParticipant
                    ? null // el profesional no se alerta a sí mismo al salir
                    : AlertMaterializer.ParticipantLeft(recipientUserId, appointmentId, patientName),
                "room-ended" => AlertMaterializer.SessionEnded(recipientUserId, appointmentId, patientName),
                _ => null,
            };

            if (alert is not null)
            {
                await alerts.AddRangeAsync([alert], ct);
            }
        }
        catch (Exception ex)
        {
            // La bandeja no debe tumbar el procesamiento del webhook (idempotencia
            // y atomicidad ya están garantizadas por la clave de evento).
            logger.LogWarning(ex, "No se pudo materializar la alerta del evento {EventType} de la cita {AppointmentId}.",
                eventType, appointmentId);
        }
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

    /// <summary>
    /// Registra el primer ingreso del paciente a la sala (identidad del token
    /// distinta a la del profesional). Si el backend no resuelve al profesional
    /// no se marca nada, para no confundir NoShow con Completed en el barrido.
    /// </summary>
    private async Task TrackPatientJoinAsync(
        VirtualRoom room,
        string participantIdentity,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (room.PatientJoinedAt is not null || string.IsNullOrWhiteSpace(participantIdentity))
        {
            return;
        }

        var appointment = await appointments.GetForUpdateAsync(room.AppointmentId, ct);
        if (appointment is null)
        {
            return;
        }

        var professional = await referenceData.GetProfessionalAsync(appointment.ProfessionalId, ct);
        if (professional?.UserId is not { } professionalUserId)
        {
            return;
        }

        if (Guid.TryParse(participantIdentity, out var participantUserId)
            && participantUserId == professionalUserId)
        {
            return;
        }

        room.PatientJoinedAt = now;
    }

    private async Task<WebhookMetricEvents> CompleteAppointmentIfInProgressAsync(
        Guid appointmentId,
        bool sessionEnded,
        long? sessionDurationSeconds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // La sala terminó: si la cita estaba en curso, pasa a completada. Si nunca
        // se inició sesión (nadie entró), se deja como está (NoShow es una
        // decisión de negocio aparte, no una conclusión automática de la sala).
        var appointment = await appointments.GetForUpdateAsync(appointmentId, ct);
        if (appointment is null)
        {
            return WebhookMetricEvents.None;
        }

        var scheduledDate = DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime);

        AppointmentStatusChangedMetricEvent? statusChanged = null;
        if (appointment.Status == AppointmentStatus.InProgress)
        {
            appointment.Status = AppointmentStatus.Completed;
            appointment.CompletedAt = now;
            appointment.UpdatedAt = now.UtcDateTime;
            await appointments.UpdateAsync(appointment, ct);

            statusChanged = new AppointmentStatusChangedMetricEvent(
                appointment.Id,
                appointment.ProfessionalId,
                appointment.ClinicId,
                scheduledDate,
                AppointmentStatus.InProgress,
                AppointmentStatus.Completed);
        }

        var sessionEndedEvent = sessionEnded
            ? new SessionEndedMetricEvent(
                appointment.Id,
                appointment.ProfessionalId,
                appointment.ClinicId,
                scheduledDate,
                sessionDurationSeconds)
            : null;

        return new WebhookMetricEvents(statusChanged, sessionEndedEvent);
    }
}
