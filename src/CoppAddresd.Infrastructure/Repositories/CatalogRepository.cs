using CoppAddresd.Application.Features.Catalogs;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class CatalogRepository(AppDbContext dbContext) : ICatalogRepository
{
    public async Task<IReadOnlyList<Country>> ListCountriesAsync(CancellationToken ct = default)
        => await dbContext.Countries
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<State>> ListStatesByCountryAsync(
        Guid countryId, CancellationToken ct = default)
        => await dbContext.States
            .AsNoTracking()
            .Where(x => x.CountryId == countryId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<City>> SearchCitiesAsync(
        Guid stateId, string? search, int limit = 30, CancellationToken ct = default)
    {
        var query = dbContext.Cities.AsNoTracking().Where(x => x.StateId == stateId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PostalCode>> ListPostalCodesByCityAsync(
        Guid cityId, CancellationToken ct = default)
        => await dbContext.PostalCodes
            .AsNoTracking()
            .Where(x => x.CityId == cityId)
            .OrderBy(x => x.ZipCode)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BloodType>> ListBloodTypesAsync(CancellationToken ct = default)
        => await dbContext.BloodTypes
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentType>> ListDocumentTypesAsync(CancellationToken ct = default)
        => await dbContext.DocumentTypes
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Ethnicity>> ListEthnicitiesAsync(CancellationToken ct = default)
        => await dbContext.Ethnicities
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Icd10Code>> SearchIcd10CodesAsync(
        string? search, int limit = 30, CancellationToken ct = default)
    {
        var query = dbContext.Icd10Codes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Code, pattern) ||
                EF.Functions.ILike(x.Description ?? string.Empty, pattern));
        }

        return await query
            .OrderBy(x => x.Code)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Medication>> SearchMedicationsAsync(
        string? search, int limit = 30, CancellationToken ct = default)
    {
        var query = dbContext.Medications.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, pattern) ||
                EF.Functions.ILike(x.Ndc ?? string.Empty, pattern));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Allergen>> SearchAllergensAsync(
        string? search, int limit = 30, CancellationToken ct = default)
    {
        var query = dbContext.Allergens.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<CatalogValidationResult> ValidateAsync(
        Guid? documentTypeId,
        Guid? ethnicityId,
        Guid? bloodTypeId,
        Guid? countryId,
        Guid? stateId,
        Guid? cityId,
        IReadOnlyCollection<Guid> insurerIds,
        IReadOnlyCollection<Guid> icd10CodeIds,
        IReadOnlyCollection<Guid> medicationIds,
        IReadOnlyCollection<Guid> allergenIds,
        CancellationToken ct = default)
    {
        var documentTypeExists = documentTypeId is null ||
            await dbContext.DocumentTypes.AsNoTracking().AnyAsync(x => x.Id == documentTypeId, ct);

        var ethnicityExists = ethnicityId is null ||
            await dbContext.Ethnicities.AsNoTracking().AnyAsync(x => x.Id == ethnicityId, ct);

        var bloodTypeExists = bloodTypeId is null ||
            await dbContext.BloodTypes.AsNoTracking().AnyAsync(x => x.Id == bloodTypeId, ct);

        var countryExists = countryId is null ||
            await dbContext.Countries.AsNoTracking().AnyAsync(x => x.Id == countryId, ct);

        var stateExists = stateId is null ||
            await dbContext.States.AsNoTracking().AnyAsync(x => x.Id == stateId, ct);

        var stateInCountry = true;
        if (stateId is not null && countryId is not null)
        {
            stateInCountry = await dbContext.States.AsNoTracking()
                .AnyAsync(x => x.Id == stateId && x.CountryId == countryId, ct);
        }

        var cityExists = cityId is null ||
            await dbContext.Cities.AsNoTracking().AnyAsync(x => x.Id == cityId, ct);

        var cityInState = true;
        if (cityId is not null && stateId is not null)
        {
            cityInState = await dbContext.Cities.AsNoTracking()
                .AnyAsync(x => x.Id == cityId && x.StateId == stateId, ct);
        }

        var existingInsurerIds = await dbContext.Insurers.AsNoTracking()
            .Where(x => insurerIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var existingIcd10CodeIds = await dbContext.Icd10Codes.AsNoTracking()
            .Where(x => icd10CodeIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var existingMedicationIds = await dbContext.Medications.AsNoTracking()
            .Where(x => medicationIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var existingAllergenIds = await dbContext.Allergens.AsNoTracking()
            .Where(x => allergenIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        return new CatalogValidationResult(
            documentTypeExists,
            ethnicityExists,
            bloodTypeExists,
            countryExists,
            stateExists,
            stateInCountry,
            cityExists,
            cityInState,
            existingInsurerIds.ToHashSet(),
            existingIcd10CodeIds.ToHashSet(),
            existingMedicationIds.ToHashSet(),
            existingAllergenIds.ToHashSet());
    }
}