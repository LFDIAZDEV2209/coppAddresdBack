using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Solicitudes de un paciente ("Mis solicitudes"), de más reciente a más antigua.
/// Alcance dual resuelto en el handler: <c>ErpMode=true</c> (usuario con permiso
/// <c>Appointments.RequestsView</c>) conserva el comportamiento histórico
/// (<c>PatientId</c> del query o el <c>ActingUserId</c> como fallback); con
/// <c>ErpMode=false</c> (paciente de la app móvil) el paciente se resuelve por
/// identidad del JWT y un <c>PatientId</c> ajeno en el query responde 403.
/// </summary>
public sealed record GetMyRequestsQuery(Guid? PatientId, Guid ActingUserId, bool ErpMode)
    : IRequest<IReadOnlyList<TelemedicineRequestDto>>;

public sealed class GetMyRequestsQueryHandler(
    IRequestRepository requests,
    IAppointmentReferenceDataService referenceData
) : IRequestHandler<GetMyRequestsQuery, IReadOnlyList<TelemedicineRequestDto>>
{
    public async Task<IReadOnlyList<TelemedicineRequestDto>> Handle(
        GetMyRequestsQuery request,
        CancellationToken ct
    )
    {
        Guid patientId;

        if (request.ErpMode)
        {
            // Comportamiento histórico del ERP: patientId del query o el id
            // del usuario como fallback (conservado tal cual).
            patientId = request.PatientId ?? request.ActingUserId;
        }
        else
        {
            var actingPatient = await referenceData.GetPatientByUserIdAsync(
                request.ActingUserId,
                ct
            );
            if (actingPatient is null)
            {
                throw new ForbiddenException(
                    "Solo los pacientes pueden consultar sus solicitudes desde la app móvil."
                );
            }

            if (request.PatientId is { } requested && requested != actingPatient.Id)
            {
                throw new ForbiddenException("Solo puedes consultar tus propias solicitudes.");
            }

            patientId = actingPatient.Id;
        }

        var items = await requests.ListByPatientAsync(patientId, ct);

        var patient = await referenceData.GetPatientAsync(patientId, ct);
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
                r.CreatedAt,
                r.RejectionReason
            ))
            .ToList();
    }
}
