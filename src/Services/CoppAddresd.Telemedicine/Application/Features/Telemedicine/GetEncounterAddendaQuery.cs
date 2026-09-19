using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Adendas de un encuentro clínico (F4), ordenadas por <c>(created_at, id)</c>.
/// Autorización idéntica a la del encuentro: profesional asignado o supervisor
/// (<c>Telemedicine.SessionsManage</c>); el paciente no accede. Si el encuentro
/// aún no existe devuelve un arreglo vacío (la UI no necesita 404).
/// </summary>
public sealed record GetEncounterAddendaQuery(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<IReadOnlyList<EncounterAddendumDto>>;

public sealed class GetEncounterAddendaQueryValidator : AbstractValidator<GetEncounterAddendaQuery>
{
    public GetEncounterAddendaQueryValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class GetEncounterAddendaQueryHandler(
    IAppointmentRepository appointments,
    IEncounterRepository encounters,
    IEncounterAddendumRepository addenda,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<GetEncounterAddendaQuery, IReadOnlyList<EncounterAddendumDto>>
{
    public async Task<IReadOnlyList<EncounterAddendumDto>> Handle(
        GetEncounterAddendaQuery request,
        CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireSessionOwnerAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        var encounter = await encounters.GetByAppointmentIdAsync(appointment.Id, ct);
        if (encounter is null)
        {
            return [];
        }

        var items = await addenda.ListByEncounterAsync(encounter.Id, ct);
        return items.Select(EncounterAddendumDto.FromEntity).ToList();
    }
}
