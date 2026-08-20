using CoppAddresd.Telemedicine.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Agenda del profesional en un rango (dashboard "Mi agenda" y calendario).
/// Devuelve las citas del rango con nombres resueltos (deduplicados por
/// entidad única para evitar N+1 sobre el backend).
/// </summary>
public sealed record GetProfessionalAgendaQuery(
    Guid ProfessionalId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<IReadOnlyList<TelemedicineAppointmentDto>>;

public sealed class GetProfessionalAgendaQueryHandler(
    IAppointmentRepository appointments,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<GetProfessionalAgendaQuery, IReadOnlyList<TelemedicineAppointmentDto>>
{
    public async Task<IReadOnlyList<TelemedicineAppointmentDto>> Handle(
        GetProfessionalAgendaQuery request,
        CancellationToken ct)
    {
        var items = await appointments.ListByProfessionalAsync(
            request.ProfessionalId, request.From, request.To, ct);

        return await TelemedicineAppointmentMapper.BuildDtosAsync(items, referenceData, ct);
    }
}
