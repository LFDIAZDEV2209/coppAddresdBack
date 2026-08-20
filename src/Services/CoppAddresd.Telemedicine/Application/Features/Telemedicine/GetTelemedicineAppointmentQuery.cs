using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Detalle de una cita, enriquecido con los datos de referencia del ERP.</summary>
public sealed record GetTelemedicineAppointmentQuery(Guid AppointmentId)
    : IRequest<TelemedicineAppointmentDto>;

public sealed class GetTelemedicineAppointmentQueryHandler(
    IAppointmentRepository appointments,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<GetTelemedicineAppointmentQuery, TelemedicineAppointmentDto>
{
    public async Task<TelemedicineAppointmentDto> Handle(
        GetTelemedicineAppointmentQuery request,
        CancellationToken ct)
    {
        var entity = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        var dto = await TelemedicineAppointmentMapper.BuildDtosAsync([entity], referenceData, ct);
        return dto[0];
    }
}
