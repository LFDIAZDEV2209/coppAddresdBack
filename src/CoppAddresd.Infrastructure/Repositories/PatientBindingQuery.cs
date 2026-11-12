using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class PatientBindingQuery(AppDbContext db) : IPatientBindingQuery
{
    public async Task<PatientBindingIdentity?> FindByDocumentAsync(string document, CancellationToken ct)
    {
        var normalized = document.Trim().ToLowerInvariant();
        var matches = await db.PatientProfiles.AsNoTracking().Where(p => p.Status == "Activo" && p.DeletedAt == null
            && p.DocumentNumber != null && p.DocumentNumber.ToLower() == normalized)
            .Select(p => new PatientBindingIdentity(p.Id, p.UserId, p.FirstName, p.DocumentNumber!, p.PhoneCountryCode, p.PhoneNumber))
            .Take(2).ToListAsync(ct);
        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task<PatientBindingIdentity?> FindByIdAsync(Guid patientId, CancellationToken ct)
    {
        var document = await db.PatientProfiles.AsNoTracking().Where(p => p.Id == patientId && p.Status == "Activo" && p.DeletedAt == null)
            .Select(p => p.DocumentNumber).SingleOrDefaultAsync(ct);
        if (document is null) return null;
        var unique = await FindByDocumentAsync(document, ct);
        return unique?.PatientId == patientId ? unique : null;
    }
}
