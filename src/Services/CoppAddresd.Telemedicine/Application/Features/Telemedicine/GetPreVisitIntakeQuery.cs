using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Pre-consulta del paciente de una cita (F4). Acceso con la misma autorización
/// de participante que la sala: profesional, paciente de la cita o supervisor
/// leen; un usuario ajeno recibe 403. Sin fila persistida devuelve
/// <c>null</c> (200 sin cuerpo) para que la UI muestre el estado vacío.
/// </summary>
public sealed record GetPreVisitIntakeQuery(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<PreVisitIntakeDto?>;

public sealed class GetPreVisitIntakeQueryValidator : AbstractValidator<GetPreVisitIntakeQuery>
{
    public GetPreVisitIntakeQueryValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class GetPreVisitIntakeQueryHandler(
    IAppointmentRepository appointments,
    IPreVisitIntakeRepository intakes,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<GetPreVisitIntakeQuery, PreVisitIntakeDto?>
{
    public async Task<PreVisitIntakeDto?> Handle(
        GetPreVisitIntakeQuery request,
        CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        // Lectura siempre disponible para los autorizados (incluso durante o
        // después de la consulta): solo la escritura está acotada al estado.
        await SessionSupport.RequireParticipantAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        var intake = await intakes.GetByAppointmentIdAsync(appointment.Id, ct);
        return intake is null ? null : PreVisitIntakeDto.FromEntity(intake);
    }
}
