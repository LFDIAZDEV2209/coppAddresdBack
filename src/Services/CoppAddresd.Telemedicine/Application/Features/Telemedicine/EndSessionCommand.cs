using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Finaliza la sesión de video de una cita: marca la sesión activa como
/// <c>Ended</c> (con duración y responsable), completa la sala en el proveedor
/// (best-effort) y pasa la cita a <c>Completed</c>. Idempotente: si no hay
/// sesión activa (finalizó por webhook u otra petición), devuelve el estado
/// actual sin error.
/// </summary>
public sealed record EndSessionCommand(
    Guid AppointmentId,
    string? EndReason,
    Guid UserId,
    bool HasManagePermission) : IRequest<TelemedicineAppointmentDto>;

public sealed class EndSessionCommandValidator : AbstractValidator<EndSessionCommand>
{
    public EndSessionCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.EndReason).MaximumLength(500);
    }
}

public sealed class EndSessionCommandHandler(
    IAppointmentRepository appointments,
    IVideoProvider videoProvider,
    ITelemedicineReferenceDataService referenceData,
    ILogger<EndSessionCommandHandler> logger)
    : IRequestHandler<EndSessionCommand, TelemedicineAppointmentDto>
{
    public async Task<TelemedicineAppointmentDto> Handle(EndSessionCommand request, CancellationToken ct)
    {
        var appointment = await appointments.GetForUpdateAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireSessionOwnerAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        var now = DateTimeOffset.UtcNow;

        var activeSession = appointment.Sessions
            .Where(s => s.Status == TelemedicineSessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (activeSession is not null)
        {
            activeSession.Status = TelemedicineSessionStatus.Ended;
            activeSession.EndedAt = now;
            activeSession.DurationSeconds = activeSession.StartedAt is { } startedAt
                ? (long)Math.Max(0, (now - startedAt).TotalSeconds)
                : null;
            activeSession.EndedBy = request.UserId;
            activeSession.EndReason = request.EndReason;

            var room = appointment.Room;
            if (room is not null)
            {
                // Completar la sala en el proveedor es best-effort: el fin de la
                // consulta (estado clínico) no debe quedar bloqueado por la
                // disponibilidad de Twilio. Si falla, la sala se completa sola o
                // vía el webhook room-ended posterior.
                try
                {
                    await videoProvider.CompleteRoomAsync(room.ProviderRoomSid, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex, "No se pudo completar la sala {RoomSid} en el proveedor; la sesión se finaliza localmente.",
                        room.ProviderRoomSid);
                }

                room.Status = VirtualRoomStatus.Ended;
                room.UpdatedAt = now.UtcDateTime;
            }

            appointment.Status = AppointmentStatus.Completed;
            appointment.UpdatedAt = now.UtcDateTime;

            await appointments.UpdateAsync(appointment, ct);
        }

        var dto = await TelemedicineAppointmentMapper.BuildDtosAsync([appointment], referenceData, ct);
        return dto[0];
    }
}
