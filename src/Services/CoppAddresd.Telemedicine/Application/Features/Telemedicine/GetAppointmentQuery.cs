using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Detalle de una cita, enriquecido con los datos de referencia del ERP.</summary>
public sealed record GetAppointmentQuery(Guid AppointmentId)
    : IRequest<AppointmentDto>;

public sealed class GetAppointmentQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<GetAppointmentQuery, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(
        GetAppointmentQuery request,
        CancellationToken ct)
    {
        var entity = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        var dto = await AppointmentMapper.BuildDtosAsync([entity], referenceData, ct);
        return dto[0];
    }
}
