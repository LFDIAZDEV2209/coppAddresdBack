using CoppAddresd.Application.Features.Telemedicine;
namespace CoppAddresd.Application.Interfaces;
public interface IAppointmentReferenceSearchRepository
{
    Task<AppointmentSearchReferencesDto> SearchAsync(string search, CancellationToken ct);
}
