using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Solicitudes de un paciente ("Mis solicitudes"), de más reciente a más antigua.</summary>
public sealed record GetMyRequestsQuery(Guid PatientId)
    : IRequest<IReadOnlyList<TelemedicineRequestDto>>;

public sealed class GetMyRequestsQueryHandler(
    IRequestRepository requests,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<GetMyRequestsQuery, IReadOnlyList<TelemedicineRequestDto>>
{
    public async Task<IReadOnlyList<TelemedicineRequestDto>> Handle(
        GetMyRequestsQuery request,
        CancellationToken ct)
    {
        var items = await requests.ListByPatientAsync(request.PatientId, ct);

        var patient = await referenceData.GetPatientAsync(request.PatientId, ct);
        var specialtyIds = items.Select(r => r.SpecialtyId).Distinct().ToList();
        var specialties = new Dictionary<Guid, SpecialtyRefDto>();
        foreach (var id in specialtyIds)
        {
            if (await referenceData.GetSpecialtyAsync(id, ct) is { } s)
            {
                specialties[id] = s;
            }
        }

        return items
            .Select(r => new TelemedicineRequestDto(
                r.Id,
                r.PatientId,
                patient?.FullName,
                r.ProfessionalId,
                r.SpecialtyId,
                specialties.GetValueOrDefault(r.SpecialtyId)?.Name,
                r.OrganizationId,
                r.ClinicId,
                r.LocationId,
                r.PreferredStart,
                r.Reason,
                r.Status,
                r.CreatedAt))
            .ToList();
    }
}
