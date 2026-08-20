using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Detalle del encuentro clínico de una cita (espacio clínico de la consulta).
/// Acceso restringido al profesional de la cita (identidad) o a un supervisor
/// (<c>Telemedicine.SessionsManage</c>); el paciente NO accede a datos clínicos.
/// 404 si la cita no existe o si aún no se creó el encuentro (creación perezosa).
/// </summary>
public sealed record GetClinicalEncounterQuery(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<ClinicalEncounterDto>;

public sealed class GetClinicalEncounterQueryHandler(
    IAppointmentRepository appointments,
    IEncounterRepository encounters,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<GetClinicalEncounterQuery, ClinicalEncounterDto>
{
    public async Task<ClinicalEncounterDto> Handle(
        GetClinicalEncounterQuery request,
        CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireSessionOwnerAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        var encounter = await encounters.GetByAppointmentIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException(
                "Registro clínico de la cita", request.AppointmentId);

        return new ClinicalEncounterDto(
            encounter.Id,
            encounter.AppointmentId,
            encounter.SessionId,
            encounter.PatientId,
            encounter.ProfessionalId,
            encounter.EncounterDate,
            encounter.Status,
            EncounterSupport.Deserialize(encounter.ClinicalData),
            encounter.Notes,
            encounter.CreatedAt,
            encounter.UpdatedAt);
    }
}
