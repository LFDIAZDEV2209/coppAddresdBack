using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class PatientRepository(AppDbContext dbContext) : IPatientRepository
{
    public async Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.PatientProfiles
            .AsNoTracking()
            .Include(x => x.Insurer)
            .Include(x => x.Diagnoses)
                .ThenInclude(d => d.Icd10Code)
            .Include(x => x.Medications)
                .ThenInclude(m => m.Medication)
            .Include(x => x.Allergies)
                .ThenInclude(a => a.Allergen)
            .Include(x => x.VitalSigns)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PatientProfile?> GetByMedicalRecordNumberAsync(string mrn, CancellationToken ct = default)
        => await dbContext.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MedicalRecordNumber == mrn, ct);

    public async Task<(IReadOnlyList<PatientProfile> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? insurerId,
        CancellationToken ct = default)
    {
        var query = dbContext.PatientProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, pattern) ||
                EF.Functions.ILike(x.LastName, pattern) ||
                EF.Functions.ILike(x.MedicalRecordNumber ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.DocumentNumber ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.Email ?? string.Empty, pattern));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (insurerId is not null)
            query = query.Where(x => x.InsurerId == insurerId);

        var total = await query.CountAsync(ct);

        var items = await query
            .Include(x => x.Insurer)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<PatientProfile> AddAsync(PatientProfile patient, CancellationToken ct = default)
    {
        dbContext.PatientProfiles.Add(patient);
        await dbContext.SaveChangesAsync(ct);
        return patient;
    }

    public async Task UpdateAsync(PatientProfile patient, CancellationToken ct = default)
    {
        // Reemplazo de agregado: borra las colecciones hijas actuales y agrega
        // las nuevas en la misma transacción (evita dejar huérfanos).
        // Con NpgsqlRetryingExecutionStrategy activa, la transacción manual debe
        // ejecutarse dentro de la estrategia para ser reintentable como una unidad.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            await dbContext.PatientDiagnoses
                .Where(x => x.PatientId == patient.Id)
                .ExecuteDeleteAsync(ct);
            await dbContext.PatientMedications
                .Where(x => x.PatientId == patient.Id)
                .ExecuteDeleteAsync(ct);
            await dbContext.PatientAllergies
                .Where(x => x.PatientId == patient.Id)
                .ExecuteDeleteAsync(ct);
            await dbContext.VitalSigns
                .Where(x => x.PatientId == patient.Id)
                .ExecuteDeleteAsync(ct);

            dbContext.Attach(patient);
            dbContext.Entry(patient).State = EntityState.Modified;

            foreach (var diagnosis in patient.Diagnoses)
                dbContext.Entry(diagnosis).State = EntityState.Added;
            foreach (var medication in patient.Medications)
                dbContext.Entry(medication).State = EntityState.Added;
            foreach (var allergy in patient.Allergies)
                dbContext.Entry(allergy).State = EntityState.Added;
            foreach (var vital in patient.VitalSigns)
                dbContext.Entry(vital).State = EntityState.Added;

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    public async Task DeleteAsync(PatientProfile patient, CancellationToken ct = default)
    {
        // Las colecciones hijas se eliminan por cascada (FK ON DELETE CASCADE).
        await dbContext.PatientProfiles
            .Where(x => x.Id == patient.Id)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await dbContext.PatientProfiles.AnyAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Insurer>> ListInsurersAsync(CancellationToken ct = default)
        => await dbContext.Insurers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<Insurer?> GetInsurerByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Insurers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Allergen> GetOrCreateAllergenAsync(string name, CancellationToken ct = default)
    {
        var normalized = name.Trim();
        var existing = await dbContext.Allergens
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == normalized, ct);
        if (existing is not null)
            return existing;

        // Carrera segura: ON CONFLICT DO NOTHING garantiza unicidad aun con
        // peticiones concurrentes; el perdedor relee la fila ganadora.
        var id = Guid.NewGuid();
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO app.allergens (id, name, created_at) VALUES ({id}, {normalized}, now()) ON CONFLICT (name) DO NOTHING""",
            ct);

        if (inserted > 0)
            return new Allergen { Id = id, Name = normalized, CreatedAt = DateTime.UtcNow };

        return await dbContext.Allergens
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Name == normalized, ct)
            ?? throw new InvalidOperationException("No se pudo registrar el alergeno en el catálogo.");
    }

    public async Task<Icd10Code> GetOrCreateIcd10CodeAsync(string code, string? description, CancellationToken ct = default)
    {
        var normalized = code.Trim();
        var existing = await dbContext.Icd10Codes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized, ct);
        if (existing is not null)
            return existing;

        var id = Guid.NewGuid();
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO app.icd10_codes (id, code, description, created_at) VALUES ({id}, {normalized}, {description}, now()) ON CONFLICT (code) DO NOTHING""",
            ct);

        if (inserted > 0)
            return new Icd10Code
            {
                Id = id,
                Code = normalized,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                CreatedAt = DateTime.UtcNow,
            };

        return await dbContext.Icd10Codes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == normalized, ct)
            ?? throw new InvalidOperationException("No se pudo registrar el código ICD-10 en el catálogo.");
    }

    public async Task<Medication> GetOrCreateMedicationAsync(
        string name, string? ndc, string? rxNorm, string? drugClass, CancellationToken ct = default)
    {
        var normalized = name.Trim();
        var existing = await dbContext.Medications
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Name == normalized, ct);
        if (existing is not null)
            return existing;

        var id = Guid.NewGuid();
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO app.medications (id, name, ndc, rx_norm, drug_class, created_at) VALUES ({id}, {normalized}, {ndc}, {rxNorm}, {drugClass}, now()) ON CONFLICT (name) DO NOTHING""",
            ct);

        if (inserted > 0)
            return new Medication
            {
                Id = id,
                Name = normalized,
                Ndc = string.IsNullOrWhiteSpace(ndc) ? null : ndc.Trim(),
                RxNorm = string.IsNullOrWhiteSpace(rxNorm) ? null : rxNorm.Trim(),
                DrugClass = string.IsNullOrWhiteSpace(drugClass) ? null : drugClass.Trim(),
                CreatedAt = DateTime.UtcNow,
            };

        return await dbContext.Medications
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == normalized, ct)
            ?? throw new InvalidOperationException("No se pudo registrar el medicamento en el catálogo.");
    }
}