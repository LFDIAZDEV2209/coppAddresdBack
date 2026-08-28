using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Detalle de una solicitud, enriquecido con datos de referencia.</summary>
public sealed record GetTelemedicineRequestQuery(Guid RequestId) : IRequest<TelemedicineRequestDto>;

public sealed class GetTelemedicineRequestQueryHandler(
    IRequestRepository requests,
    IAppointmentReferenceDataService referenceData
) : IRequestHandler<GetTelemedicineRequestQuery, TelemedicineRequestDto>
{
    public async Task<TelemedicineRequestDto> Handle(
        GetTelemedicineRequestQuery request,
        CancellationToken ct
    )
    {
        var entity =
            await requests.GetByIdAsync(request.RequestId, ct)
            ?? throw new NotFoundException("Solicitud", request.RequestId);

        PatientRefDto? patient = null;
        var specialtyName = (await referenceData.GetSpecialtyAsync(entity.SpecialtyId, ct))?.Name;
        if (entity.PatientId != Guid.Empty)
        {
            patient = await referenceData.GetPatientAsync(entity.PatientId, ct);
        }

        return new TelemedicineRequestDto(
            entity.Id,
            entity.PatientId,
            patient?.FullName,
            entity.ProfessionalId,
            entity.SpecialtyId,
            specialtyName,
            entity.OrganizationId,
            entity.ClinicId,
            entity.LocationId,
            entity.PreferredStart,
            entity.Reason,
            entity.Status,
            entity.CreatedAt,
            entity.RejectionReason
        );
    }
}
