using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Detalle del encuentro clínico de una cita (espacio clínico de la consulta).
/// Acceso restringido al profesional de la cita (identidad) o a un supervisor
/// (<c>Telemedicine.SessionsManage</c>); el paciente NO accede a datos clínicos.
/// 404 solo si la cita no existe; si el encuentro aún no se creó (creación
/// perezosa) se devuelve un borrador vacío para que la UI no reciba 404.
/// </summary>
public sealed record GetClinicalEncounterQuery(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<ClinicalEncounterDto>;

public sealed class GetClinicalEncounterQueryHandler(
    IAppointmentRepository appointments,
    IEncounterRepository encounters,
    IAppointmentReferenceDataService referenceData)
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

        var encounter = await encounters.GetByAppointmentIdAsync(request.AppointmentId, ct);
        if (encounter is null)
        {
            // Creación perezosa: sin fila persistida devolvemos un borrador
            // vacío (Id vacío) en vez de 404; el primer guardado lo crea real.
            return new ClinicalEncounterDto(
                Guid.Empty,
                appointment.Id,
                SessionId: null,
                appointment.PatientId,
                appointment.ProfessionalId,
                appointment.ScheduledStart,
                EncounterStatus.Draft,
                ClinicalData: null,
                Notes: null,
                appointment.CreatedAt,
                UpdatedAt: null);
        }

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
