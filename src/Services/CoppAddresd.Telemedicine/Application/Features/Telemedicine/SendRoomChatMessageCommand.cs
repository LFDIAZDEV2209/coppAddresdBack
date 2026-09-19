using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Envía un mensaje al chat de la consulta (F3). La autorización es la misma de
/// la sala (profesional/paciente de la cita o supervisor con
/// <c>Telemedicine.SessionsManage</c>); el emisor y su rol salen del JWT, nunca
/// del cuerpo. Solo se permite con la cita confirmada, en curso o completada.
/// </summary>
public sealed record SendRoomChatMessageCommand(
    Guid AppointmentId,
    string Body,
    Guid UserId,
    bool HasManagePermission) : IRequest<ChatMessageDto>;

public sealed class SendRoomChatMessageCommandValidator : AbstractValidator<SendRoomChatMessageCommand>
{
    /// <summary>Longitud máxima del cuerpo de un mensaje (coincide con la columna).</summary>
    public const int MaxBodyLength = 2000;

    public SendRoomChatMessageCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Body)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("El mensaje no puede estar vacío.")
            .Must(body => body.Trim().Length > 0)
            .WithMessage("El mensaje no puede estar vacío.")
            .Must(body => body.Trim().Length <= MaxBodyLength)
            .WithMessage($"El mensaje no puede superar los {MaxBodyLength} caracteres.");
    }
}

public sealed class SendRoomChatMessageCommandHandler(
    IAppointmentRepository appointments,
    IChatMessageRepository messages,
    IAppointmentReferenceDataService referenceData,
    ILogger<SendRoomChatMessageCommandHandler> logger,
    ITelemedicineMetricsQueue? metricsQueue = null)
    : IRequestHandler<SendRoomChatMessageCommand, ChatMessageDto>
{
    public async Task<ChatMessageDto> Handle(SendRoomChatMessageCommand request, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        var role = await SessionSupport.RequireParticipantAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        SessionSupport.EnsureChatAllowed(appointment.Status);

        var message = await messages.AddAsync(
            new ChatMessage
            {
                AppointmentId = appointment.Id,
                SenderUserId = request.UserId,
                SenderRole = role.ToString(),
                Body = request.Body.Trim(),
                CreatedAt = DateTime.UtcNow,
            },
            ct);

        // F5: contador de mensajes por rol (sin PHI: solo el rol derivado del JWT).
        if (metricsQueue is not null)
        {
            await metricsQueue.EnqueueAsync(new ChatMessageSentMetricEvent(
                appointment.Id,
                appointment.ProfessionalId,
                appointment.ClinicId,
                DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime),
                role.ToString()));
        }

        // Log sin PHI: solo ids, rol y longitud del mensaje, nunca el contenido.
        logger.LogInformation(
            "Mensaje de chat {MessageId} creado en la cita {AppointmentId} por {SenderRole} ({BodyLength} caracteres).",
            message.Id, message.AppointmentId, message.SenderRole, message.Body.Length);

        return ChatMessageDto.FromEntity(message);
    }
}
