using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class PatientRepository(AppDbContext dbContext) : IPatientRepository
{
    public async Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await QueryDetail()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    private IQueryable<PatientProfile> QueryDetail()
        => dbContext.PatientProfiles
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Include(x => x.Insurer)
            .Include(x => x.DocumentType)
            .Include(x => x.Ethnicity)
            .Include(x => x.BloodType)
            .Include(x => x.Country)
            .Include(x => x.State)
            .Include(x => x.City)
            .Include(x => x.Clinic)
            .Include(x => x.Location)
            .Include(x => x.Diagnoses)
                .ThenInclude(d => d.Icd10Code)
            .Include(x => x.Medications)
                .ThenInclude(m => m.Medication)
            .Include(x => x.Allergies)
                .ThenInclude(a => a.Allergen)
            .Include(x => x.VitalSigns);

    public async Task<PatientProfile?> GetByMedicalRecordNumberAsync(string mrn, CancellationToken ct = default)
        => await dbContext.PatientProfiles
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .FirstOrDefaultAsync(x => x.MedicalRecordNumber == mrn, ct);

    public async Task<(IReadOnlyList<PatientProfile> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? insurerId,
        Guid? clinicId,
        CancellationToken ct = default)
    {
        var query = dbContext.PatientProfiles.AsNoTracking()
            .Where(x => x.DeletedAt == null);

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

        // Frontera de datos (Fase 4): con clínica activa solo se ven sus
        // pacientes. El directorio legacy sin clínica (clinic_id null) queda
        // fuera de la vista por clínica; se accede desde el contexto global.
        if (clinicId is not null)
            query = query.Where(x => x.ClinicId == clinicId);

        var total = await query.CountAsync(ct);

        var items = await query
            .Include(x => x.Insurer)
            .Include(x => x.DocumentType)
            .Include(x => x.Clinic)
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

    public async Task SoftDeleteAsync(PatientProfile patient, CancellationToken ct = default)
    {
        // Soft delete: se marca deleted_at y se conservan las filas hijas
        // (trazabilidad PHI). Nunca se eliminan físicamente los registros.
        await dbContext.PatientProfiles
            .Where(x => x.Id == patient.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DeletedAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedBy, patient.UpdatedBy), ct);
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
}