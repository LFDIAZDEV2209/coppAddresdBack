using CoppAddresd.Application.Features.Telemedicine;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CoppAddresd.Infrastructure.Repositories;
/// <summary>Búsqueda batch de referencias antes de paginar las citas. Solo proyecta IDs.</summary>
public sealed class AppointmentReferenceSearchRepository(AppDbContext db) : IAppointmentReferenceSearchRepository
{
    public async Task<AppointmentSearchReferencesDto> SearchAsync(string search, CancellationToken ct)
    {
        var term = search.ToLowerInvariant();
        var patients = await db.PatientProfiles.AsNoTracking().Where(p => p.DeletedAt == null &&
            ((p.FirstName + " " + (p.MiddleName == null || p.MiddleName == "" ? "" : p.MiddleName + " ") + p.LastName).ToLower().Contains(term) || (p.DocumentNumber ?? "").ToLower().Contains(term))).Select(p => p.Id).ToListAsync(ct);
        var professionals = await db.Professionals.AsNoTracking().Where(p =>
            (p.Employee.FirstName + " " + (p.Employee.MiddleName == null || p.Employee.MiddleName == "" ? "" : p.Employee.MiddleName + " ") + p.Employee.LastName).ToLower().Contains(term)).Select(p => p.Id).ToListAsync(ct);
        var specialties = await db.Specialties.AsNoTracking().Where(s => s.Name.ToLower().Contains(term)).Select(s => s.Id).ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking().Where(s => s.Name.ToLower().Contains(term)).Select(s => s.Id).ToListAsync(ct);
        return new(patients, professionals, specialties, locations);
    }
}
