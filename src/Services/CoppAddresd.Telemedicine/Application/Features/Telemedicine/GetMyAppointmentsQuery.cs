using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Citas del paciente autenticado ("Mis citas"), paginadas y con filtros por
/// estado y rango. El paciente se resuelve por la identidad del JWT
/// (<c>ActingUserId</c>), nunca por un id del cliente; sin perfil de paciente
/// resoluble → 403.
/// </summary>
public sealed record GetMyAppointmentsQuery(
    Guid ActingUserId,
    AppointmentStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize
) : IRequest<PaginatedAdminAppointmentsResult>;

public sealed class GetMyAppointmentsQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData
) : IRequestHandler<GetMyAppointmentsQuery, PaginatedAdminAppointmentsResult>
{
    public async Task<PaginatedAdminAppointmentsResult> Handle(
        GetMyAppointmentsQuery request,
        CancellationToken ct
    )
    {
        var patient = await referenceData.GetPatientByUserIdAsync(request.ActingUserId, ct);
        if (patient is null)
        {
            throw new ForbiddenException(
                "Solo los pacientes pueden consultar sus citas desde la app móvil."
            );
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await appointments.ListAdminAsync(
            professionalId: null,
            patientId: patient.Id,
            clinicId: null,
            locationId: null,
            status: request.Status,
            from: request.From?.ToUniversalTime(),
            to: request.To?.ToUniversalTime(),
            page,
            pageSize,
            ct
        );

        var dtos = await AppointmentMapper.BuildDtosAsync(items, referenceData, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdminAppointmentsResult(dtos, total, page, pageSize, totalPages);
    }
}
