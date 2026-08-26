using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF de la estructura organizacional del ERP (schema <c>erp</c>).
/// Lecturas con <c>AsNoTracking</c> y proyección mínima; el árbol se carga con
/// Includes de un solo nivel por rama (organización → clínicas → sedes).
/// </summary>
public sealed class OrganizationRepository(AppDbContext dbContext) : IOrganizationRepository
{
    public async Task<IReadOnlyList<Organization>> ListOrganizationsAsync(
        CancellationToken ct = default
    ) => await dbContext.Organizations.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Organization>> ListTreeAsync(CancellationToken ct = default) =>
        await dbContext
            .Organizations.AsNoTracking()
            .Include(x => x.Clinics)
                .ThenInclude(c => c.Locations)
                    .ThenInclude(l => l.City)
            .Include(x => x.Clinics)
                .ThenInclude(c => c.Locations)
                    .ThenInclude(l => l.State)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<Organization?> GetOrganizationByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) => await dbContext.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Organization> AddOrganizationAsync(
        Organization organization,
        CancellationToken ct = default
    )
    {
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync(ct);
        return organization;
    }

    public async Task UpdateOrganizationAsync(
        Organization organization,
        CancellationToken ct = default
    )
    {
        // Update dirigido: evita trackear la entidad completa (regla EF del proyecto).
        await dbContext
            .Organizations.Where(x => x.Id == organization.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Name, organization.Name)
                        .SetProperty(x => x.IsActive, organization.IsActive)
                        .SetProperty(x => x.UpdatedAt, organization.UpdatedAt),
                ct
            );
    }

    public async Task<Clinic?> GetClinicByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Clinic> AddClinicAsync(Clinic clinic, CancellationToken ct = default)
    {
        dbContext.Clinics.Add(clinic);
        await dbContext.SaveChangesAsync(ct);
        return clinic;
    }

    public async Task UpdateClinicAsync(Clinic clinic, CancellationToken ct = default)
    {
        await dbContext
            .Clinics.Where(x => x.Id == clinic.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Name, clinic.Name)
                        .SetProperty(x => x.Code, clinic.Code)
                        .SetProperty(x => x.IsActive, clinic.IsActive)
                        .SetProperty(x => x.UpdatedAt, clinic.UpdatedAt),
                ct
            );
    }

    public async Task<Location?> GetLocationByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Locations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Location> AddLocationAsync(Location location, CancellationToken ct = default)
    {
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(ct);
        return location;
    }

    public async Task UpdateLocationAsync(Location location, CancellationToken ct = default)
    {
        await dbContext
            .Locations.Where(x => x.Id == location.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Name, location.Name)
                        .SetProperty(x => x.AddressLine1, location.AddressLine1)
                        .SetProperty(x => x.AddressLine2, location.AddressLine2)
                        .SetProperty(x => x.CityId, location.CityId)
                        .SetProperty(x => x.StateId, location.StateId)
                        .SetProperty(x => x.PostalCode, location.PostalCode)
                        .SetProperty(x => x.PhoneCountryCode, location.PhoneCountryCode)
                        .SetProperty(x => x.PhoneNumber, location.PhoneNumber)
                        .SetProperty(x => x.IsActive, location.IsActive)
                        .SetProperty(x => x.UpdatedAt, location.UpdatedAt),
                ct
            );
    }

    public async Task<IReadOnlyList<ProfessionalType>> ListProfessionalTypesAsync(
        CancellationToken ct = default
    ) =>
        await dbContext
            .ProfessionalTypes.AsNoTracking()
            .Include(x => x.Specialties)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Specialty>> ListSpecialtiesAsync(
        CancellationToken ct = default
    ) =>
        await dbContext
            .Specialties.AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Specialty>> ListSpecialtiesByProfessionalTypeAsync(
        Guid professionalTypeId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .ProfessionalTypeSpecialties.AsNoTracking()
            .Where(x => x.ProfessionalTypeId == professionalTypeId)
            .Select(x => x.Specialty)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<ProfessionalType?> GetProfessionalTypeByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) => await dbContext.ProfessionalTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Specialty?> GetSpecialtyByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Specialties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ProfessionalTypeCodeExistsAsync(
        string code,
        CancellationToken ct = default
    ) => await dbContext.ProfessionalTypes.AnyAsync(x => x.Code == code, ct);

    public async Task<bool> SpecialtyCodeExistsAsync(string code, CancellationToken ct = default) =>
        await dbContext.Specialties.AnyAsync(x => x.Code == code, ct);

    public async Task<ProfessionalType> AddProfessionalTypeAsync(
        ProfessionalType type,
        CancellationToken ct = default
    )
    {
        dbContext.ProfessionalTypes.Add(type);
        await dbContext.SaveChangesAsync(ct);
        return type;
    }

    public async Task UpdateProfessionalTypeAsync(
        ProfessionalType type,
        CancellationToken ct = default
    )
    {
        dbContext.ProfessionalTypes.Update(type);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<Specialty> AddSpecialtyAsync(
        Specialty specialty,
        CancellationToken ct = default
    )
    {
        dbContext.Specialties.Add(specialty);
        await dbContext.SaveChangesAsync(ct);
        return specialty;
    }

    public async Task UpdateSpecialtyAsync(Specialty specialty, CancellationToken ct = default)
    {
        dbContext.Specialties.Update(specialty);
        await dbContext.SaveChangesAsync(ct);
    }
}
