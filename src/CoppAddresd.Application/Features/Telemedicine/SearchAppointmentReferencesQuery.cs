using CoppAddresd.Application.Interfaces;
using MediatR;
namespace CoppAddresd.Application.Features.Telemedicine;
public record AppointmentSearchReferencesDto(IReadOnlyList<Guid> PatientIds, IReadOnlyList<Guid> ProfessionalIds, IReadOnlyList<Guid> SpecialtyIds, IReadOnlyList<Guid> LocationIds);
public record SearchAppointmentReferencesQuery(string Search) : IRequest<AppointmentSearchReferencesDto>;
public sealed class SearchAppointmentReferencesQueryHandler(IAppointmentReferenceSearchRepository repository)
    : IRequestHandler<SearchAppointmentReferencesQuery, AppointmentSearchReferencesDto>
{
    public Task<AppointmentSearchReferencesDto> Handle(SearchAppointmentReferencesQuery request, CancellationToken ct)
    {
        var term = request.Search.Trim();
        if (term.Length is < 1 or > 100) throw new ArgumentException("Búsqueda inválida.");
        return repository.SearchAsync(term, ct);
    }
}
