using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class PatientRepository(AppDbContext dbContext) : IPatientRepository
{
    public async Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await QueryDetail().FirstOrDefaultAsync(x => x.Id == id, ct);

    private IQueryable<PatientProfile> QueryDetail() =>
        dbContext
            .PatientProfiles.AsNoTracking()
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
            .Include(x => x.VitalSigns)
            .Include(x => x.Assignments)
                .ThenInclude(a => a.Professional)
                    .ThenInclude(p => p.Employee);

    public async Task<PatientProfile?> GetByMedicalRecordNumberAsync(
        string mrn,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .FirstOrDefaultAsync(x => x.MedicalRecordNumber == mrn, ct);

    public async Task<PatientProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.UserId == userId)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<PatientProfile> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? insurerId,
        Guid? clinicId,
        Guid? professionalId,
        string? sortBy,
        string? sortDir,
        string? stateCode = null,
        CancellationToken ct = default
    )
    {
        var query = dbContext.PatientProfiles.AsNoTracking().Where(x => x.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, pattern)
                || EF.Functions.ILike(x.LastName, pattern)
                || EF.Functions.ILike(x.MedicalRecordNumber ?? string.Empty, pattern)
                || EF.Functions.ILike(x.DocumentNumber ?? string.Empty, pattern)
                || EF.Functions.ILike(x.Email ?? string.Empty, pattern)
            );
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (insurerId is not null)
            query = query.Where(x => x.InsurerId == insurerId);

        // Filtro por estado de EE. UU. (selección del mapa geográfico): solo
        // pacientes con ese estado registrado.
        if (!string.IsNullOrWhiteSpace(stateCode))
            query = query.Where(x => x.State != null && x.State.Code == stateCode);

        // Frontera de datos (Fase 4): con clínica activa solo se ven sus
        // pacientes. El directorio legacy sin clínica (clinic_id null) queda
        // fuera de la vista por clínica; se accede desde el contexto global.
        if (clinicId is not null)
            query = query.Where(x => x.ClinicId == clinicId);

        // Alcance "propios" (profesional clínico): solo pacientes con una
        // asignación activa hacia él. El id NUNCA viene del cliente: lo
        // resuelve el backend desde la identidad del JWT.
        if (professionalId is not null)
            query = query.Where(x =>
                x.Assignments.Any(a => a.ProfessionalId == professionalId && a.Status == "Active")
            );

        var total = await query.CountAsync(ct);

        // Orden server-side por whitelist de campos (el handler ya valida que
        // sortBy esté en PatientSortFields.All; null → CreatedAt desc). El
        // orden se aplica con expressions tipadas, nunca por interpolación.
        // Desempate estable por Id en todas las ramas (paginación estable).
        IOrderedQueryable<PatientProfile> ordered = sortBy switch
        {
            PatientSortFields.FirstName => sortDir == "asc"
                ? query
                    .OrderBy(x => x.FirstName)
                    .ThenBy(x => x.LastName)
                    .ThenByDescending(x => x.Id)
                : query
                    .OrderByDescending(x => x.FirstName)
                    .ThenByDescending(x => x.LastName)
                    .ThenByDescending(x => x.Id),
            PatientSortFields.DocumentNumber => sortDir == "asc"
                ? query.OrderBy(x => x.DocumentNumber).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.DocumentNumber).ThenByDescending(x => x.Id),
            PatientSortFields.PhoneNumber => sortDir == "asc"
                ? query.OrderBy(x => x.PhoneNumber).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.PhoneNumber).ThenByDescending(x => x.Id),
            PatientSortFields.InsurerName => sortDir == "asc"
                ? query.OrderBy(x => x.Insurer!.Name).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.Insurer!.Name).ThenByDescending(x => x.Id),
            PatientSortFields.ClinicName => sortDir == "asc"
                ? query.OrderBy(x => x.Clinic!.Name).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.Clinic!.Name).ThenByDescending(x => x.Id),
            PatientSortFields.Status => sortDir == "asc"
                ? query.OrderBy(x => x.Status).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.Status).ThenByDescending(x => x.Id),
            _ => sortDir == "asc"
                ? query.OrderBy(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
        };

        var items = await ordered
            .Include(x => x.Insurer)
            .Include(x => x.DocumentType)
            .Include(x => x.Clinic)
            .Include(x => x.State)
            // Diagnósticos de la página para "diagnóstico principal": EF emite
            // una query separada tras la paginación (split), sin N+1 por fila.
            .Include(x => x.Diagnoses)
                .ThenInclude(d => d.Icd10Code)
            // Asignaciones de la página para la columna "Profesional": EF emite
            // una query separada tras la paginación (split automático con
            // Skip/Take), sin producto cartesiano.
            .Include(x => x.Assignments)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<PatientProfile> AddAsync(
        PatientProfile patient,
        CancellationToken ct = default
    )
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

            await dbContext
                .PatientDiagnoses.Where(x => x.PatientId == patient.Id)
                .ExecuteDeleteAsync(ct);
            await dbContext
                .PatientMedications.Where(x => x.PatientId == patient.Id)
                .ExecuteDeleteAsync(ct);
            await dbContext
                .PatientAllergies.Where(x => x.PatientId == patient.Id)
                .ExecuteDeleteAsync(ct);
            await dbContext.VitalSigns.Where(x => x.PatientId == patient.Id).ExecuteDeleteAsync(ct);

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
        await dbContext
            .PatientProfiles.Where(x => x.Id == patient.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.DeletedAt, DateTime.UtcNow)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(x => x.UpdatedBy, patient.UpdatedBy),
                ct
            );
    }

    public async Task<PatientStatusSnapshot?> GetStatusSnapshotAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null)
            .Select(x => new PatientStatusSnapshot(x.Status, x.ClinicId))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> UpdateStatusAsync(
        Guid id,
        string status,
        Guid? updatedBy,
        CancellationToken ct = default
    )
    {
        // ExecuteUpdate: solo status + auditoría; el resto del agregado y sus
        // colecciones hijas quedan intactos (el toggle nunca borra datos).
        var affected = await dbContext
            .PatientProfiles.Where(x => x.Id == id && x.DeletedAt == null)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Status, status)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(x => x.UpdatedBy, updatedBy),
                ct
            );

        return affected > 0;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.PatientProfiles.AnyAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Insurer>> ListInsurersAsync(CancellationToken ct = default) =>
        await dbContext.Insurers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<Insurer?> GetInsurerByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Insurers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PatientStatsDto> GetStatsAsync(
        Guid? clinicId,
        Guid? professionalId,
        DateTime monthStartUtc,
        CancellationToken ct = default
    )
    {
        var targetClinicId = clinicId ?? Guid.Empty;
        var monthStartDate = DateOnly.FromDateTime(monthStartUtc);

        // Pre-agregación CQRS (Fase 1): lectura O(1) desde patient_daily_metrics para vista administrativa/clínica
        if (professionalId is null)
        {
            var metrics = await dbContext
                .PatientDailyMetrics.AsNoTracking()
                .Where(m => m.ClinicId == targetClinicId)
                .ToListAsync(ct);

            if (metrics.Count > 0)
            {
                var total = (int)
                    metrics.Where(m => m.MetricKey == "total_patients").Sum(m => m.TotalCount);
                var active = (int)
                    metrics
                        .Where(m => m.MetricKey == "status_count" && m.DimensionKey == "Activo")
                        .Sum(m => m.TotalCount);
                var newThisMonth = (int)
                    metrics
                        .Where(m => m.MetricKey == "new_patients" && m.MetricDate >= monthStartDate)
                        .Sum(m => m.TotalCount);
                var unassigned = (int)
                    metrics.Where(m => m.MetricKey == "unassigned_patients").Sum(m => m.TotalCount);

                return new PatientStatsDto(total, active, newThisMonth, Math.Max(0, unassigned));
            }
        }

        var query = dbContext.PatientProfiles.AsNoTracking().Where(x => x.DeletedAt == null);

        // Misma frontera de datos que ListAsync: clínica activa + alcance
        // "propio" (asignación activa hacia el profesional del JWT).
        if (clinicId is not null)
            query = query.Where(x => x.ClinicId == clinicId);

        if (professionalId is not null)
            query = query.Where(x =>
                x.Assignments.Any(a => a.ProfessionalId == professionalId && a.Status == "Active")
            );

        // Un solo roundtrip: GROUP BY constante con agregados condicionales.
        var stats = await query
            .GroupBy(x => 1)
            .Select(g => new PatientStatsDto(
                Total: g.Count(),
                Active: g.Count(x => x.Status == "Activo"),
                NewThisMonth: g.Count(x => x.CreatedAt >= monthStartUtc),
                WithoutProfessional: g.Count(x => !x.Assignments.Any(a => a.Status == "Active"))
            ))
            .FirstOrDefaultAsync(ct);

        return stats ?? new PatientStatsDto(0, 0, 0, 0);
    }

    public async Task<bool> IsAssignedToProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfessionalAssignments.AsNoTracking()
            .AnyAsync(
                a =>
                    a.PatientId == patientId
                    && a.ProfessionalId == professionalId
                    && a.Status == "Active",
                ct
            );

    public async Task AssignProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        Guid? clinicId,
        string relationshipType,
        Guid? createdBy,
        CancellationToken ct = default
    )
    {
        var existing = await dbContext.PatientProfessionalAssignments.FirstOrDefaultAsync(
            a => a.PatientId == patientId && a.ProfessionalId == professionalId,
            ct
        );

        if (existing is null)
        {
            dbContext.PatientProfessionalAssignments.Add(
                new PatientProfessionalAssignment
                {
                    PatientId = patientId,
                    ProfessionalId = professionalId,
                    ClinicId = clinicId,
                    RelationshipType = relationshipType,
                    Status = "Active",
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }
        else
        {
            // Idempotente: reactiva la asignación existente.
            existing.Status = "Active";
            existing.RelationshipType = relationshipType;
            existing.ClinicId = clinicId;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task RemoveProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        CancellationToken ct = default
    )
    {
        // Soft: conserva la trazabilidad de la asignación (historial clínico).
        await dbContext
            .PatientProfessionalAssignments.Where(a =>
                a.PatientId == patientId && a.ProfessionalId == professionalId
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(a => a.Status, "Inactive")
                        .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                ct
            );
    }

    public async Task<IReadOnlyList<PatientProfessionalAssignmentView>> ListAssignmentsAsync(
        Guid patientId,
        CancellationToken ct = default
    )
    {
        var assignments = await dbContext
            .PatientProfessionalAssignments.AsNoTracking()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.Status == "Active")
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        if (assignments.Count == 0)
        {
            return [];
        }

        // El nombre del profesional vive en erp.employees (núcleo HR); la
        // asignación referencia erp.professionals. Dos consultas: sin N+1.
        var professionalIds = assignments.Select(a => a.ProfessionalId).Distinct().ToList();

        var employees = await dbContext
            .Employees.AsNoTracking()
            .Where(e => e.Professional != null && professionalIds.Contains(e.Professional!.Id))
            .Select(e => new
            {
                e.Professional!.Id,
                FullName = $"{e.FirstName} {e.MiddleName} {e.LastName}".Trim(),
                TypeName = e.Professional!.ProfessionalType != null
                    ? e.Professional.ProfessionalType.Name
                    : null,
            })
            .ToListAsync(ct);

        var byId = employees.ToDictionary(x => x.Id);

        return assignments
            .Select(a => new PatientProfessionalAssignmentView(
                a.ProfessionalId,
                byId.GetValueOrDefault(a.ProfessionalId)?.FullName ?? "Profesional",
                byId.GetValueOrDefault(a.ProfessionalId)?.TypeName,
                a.RelationshipType,
                a.Status,
                a.CreatedAt
            ))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetProfessionalNamesAsync(
        IReadOnlyCollection<Guid> professionalIds,
        CancellationToken ct = default
    )
    {
        if (professionalIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Una sola consulta agrupada contra el núcleo HR (erp.employees); el
        // nombre del profesional nunca vive en app.patient_professionals.
        var ids = professionalIds.Distinct().ToList();
        var employees = await dbContext
            .Employees.AsNoTracking()
            .Where(e => e.Professional != null && ids.Contains(e.Professional!.Id))
            .Select(e => new
            {
                e.Professional!.Id,
                FullName = string.Join(
                    " ",
                    new[] { e.FirstName, e.MiddleName, e.LastName }.Where(s =>
                        !string.IsNullOrWhiteSpace(s)
                    )
                ),
            })
            .ToListAsync(ct);

        return employees.ToDictionary(x => x.Id, x => x.FullName);
    }

    public async Task<IReadOnlyList<string>> GetExistingDocumentNumbersAsync(
        IReadOnlyCollection<string> documentNumbers,
        CancellationToken ct = default
    )
    {
        if (documentNumbers.Count == 0)
        {
            return [];
        }

        // Una sola query: LOWER(document_number) IN (...) para bulk duplicate check.
        var lowered = documentNumbers.Select(d => d.Trim().ToLowerInvariant()).Distinct().ToList();

        return await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.DocumentNumber != null)
            .Where(x => lowered.Contains(x.DocumentNumber!.ToLower()))
            .Select(x => x.DocumentNumber!.ToLower())
            .ToListAsync(ct);
    }
}
