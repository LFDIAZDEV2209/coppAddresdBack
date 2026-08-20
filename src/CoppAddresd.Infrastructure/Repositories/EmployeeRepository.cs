using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF del directorio de empleados (schema <c>erp</c>). La
/// actualización reemplaza las colecciones hijas (clínicas, especialidades,
/// licencias) dentro de una transacción reintentable, igual que el patrón del
/// módulo de pacientes: nunca deja huérfanos y respeta la estrategia de retry
/// de Npgsql.
/// </summary>
public sealed class EmployeeRepository(AppDbContext dbContext) : IEmployeeRepository
{
    public async Task<(IReadOnlyList<Employee> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? organizationId,
        Guid? clinicId,
        CancellationToken ct = default)
    {
        var query = dbContext.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, pattern) ||
                EF.Functions.ILike(x.LastName, pattern) ||
                EF.Functions.ILike(x.Email, pattern) ||
                EF.Functions.ILike(x.JobTitle ?? string.Empty, pattern));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (organizationId is not null)
            query = query.Where(x => x.OrganizationId == organizationId);

        if (clinicId is not null)
            query = query.Where(x =>
                x.ClinicAssignments.Any(a => a.ClinicId == clinicId && a.Status == "Active"));

        var total = await query.CountAsync(ct);

        var items = await query
            .Include(x => x.Professional)
                .ThenInclude(p => p!.ProfessionalType)
            .Include(x => x.ClinicAssignments)
                .ThenInclude(a => a.Clinic)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await QueryDetail().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Employee?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await QueryDetail().FirstOrDefaultAsync(x => x.UserId == userId, ct);

    private IQueryable<Employee> QueryDetail()
        => dbContext.Employees
            .AsNoTracking()
            .Include(x => x.Organization)
            .Include(x => x.ClinicAssignments)
                .ThenInclude(a => a.Clinic)
            .Include(x => x.ClinicAssignments)
                .ThenInclude(a => a.Clinic.Locations)
            .Include(x => x.Professional)
                .ThenInclude(p => p!.ProfessionalType)
            .Include(x => x.Professional)
                .ThenInclude(p => p!.Specialties)
                    .ThenInclude(s => s.Specialty)
            .Include(x => x.Professional)
                .ThenInclude(p => p!.Licenses)
                    .ThenInclude(l => l.Specialty);

    public async Task<bool> EmailExistsInOrganizationAsync(
        Guid organizationId,
        string email,
        Guid? excludeEmployeeId = null,
        CancellationToken ct = default)
        => await dbContext.Employees
            .AnyAsync(x => x.OrganizationId == organizationId
                && x.Email == email
                && (excludeEmployeeId == null || x.Id != excludeEmployeeId), ct);

    public async Task<Employee> AddAsync(Employee employee, CancellationToken ct = default)
    {
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync(ct);
        return employee;
    }

    public async Task SetUserIdAsync(Guid employeeId, Guid userId, CancellationToken ct = default)
    {
        await dbContext.Employees
            .Where(x => x.Id == employeeId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.UserId, userId)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task CompleteOnboardingAsync(
        Guid employeeId,
        Guid? professionalTypeId,
        string? bio,
        string? photoStorageKey,
        string? phoneCountryCode,
        string? phoneNumber,
        IReadOnlyList<Guid> specialtyIds,
        IReadOnlyList<LicenseInput> licenses,
        bool completeOnboarding,
        CancellationToken ct = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            // Datos HR del empleado (teléfono y, al completar onboarding, estado).
            await dbContext.Employees
                .Where(x => x.Id == employeeId)
                .ExecuteUpdateAsync(setters =>
                {
                    setters
                        .SetProperty(x => x.PhoneCountryCode, phoneCountryCode)
                        .SetProperty(x => x.PhoneNumber, phoneNumber)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow);

                    if (completeOnboarding)
                    {
                        setters.SetProperty(x => x.Status, "Active");
                    }
                }, ct);

            // Extensión profesional: reemplazo (tipo, bio, foto, especialidades, licencias).
            var existingProfessionalId = await dbContext.Professionals
                .Where(p => p.EmployeeId == employeeId)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            var now = DateTime.UtcNow;
            var professionalId = existingProfessionalId ?? Guid.NewGuid();

            if (existingProfessionalId is null)
            {
                dbContext.Professionals.Add(new Professional
                {
                    Id = professionalId,
                    EmployeeId = employeeId,
                    ProfessionalTypeId = professionalTypeId,
                    Bio = ProfessionalOptions.Normalize(bio),
                    PhotoStorageKey = ProfessionalOptions.Normalize(photoStorageKey),
                    OnboardingCompletedAt = completeOnboarding ? now : null,
                    CreatedAt = now,
                });
            }
            else
            {
                await dbContext.Professionals
                    .Where(p => p.Id == existingProfessionalId)
                    .ExecuteUpdateAsync(setters =>
                    {
                        setters
                            .SetProperty(p => p.ProfessionalTypeId, professionalTypeId)
                            .SetProperty(p => p.Bio, ProfessionalOptions.Normalize(bio))
                            .SetProperty(p => p.PhotoStorageKey, ProfessionalOptions.Normalize(photoStorageKey))
                            .SetProperty(p => p.UpdatedAt, now);

                        if (completeOnboarding)
                        {
                            setters.SetProperty(p => p.OnboardingCompletedAt, now);
                        }
                    }, ct);

                await dbContext.ProfessionalSpecialties
                    .Where(s => s.ProfessionalId == existingProfessionalId)
                    .ExecuteDeleteAsync(ct);
                await dbContext.ProfessionalLicenses
                    .Where(l => l.ProfessionalId == existingProfessionalId)
                    .ExecuteDeleteAsync(ct);
            }

            dbContext.ProfessionalSpecialties.AddRange(
                specialtyIds.Distinct().Select(specialtyId => new ProfessionalSpecialty
                {
                    ProfessionalId = professionalId,
                    SpecialtyId = specialtyId,
                    IsPrimary = false,
                    CreatedAt = now,
                }));

            dbContext.ProfessionalLicenses.AddRange(
                licenses.Select(l => new ProfessionalLicense
                {
                    Id = Guid.NewGuid(),
                    ProfessionalId = professionalId,
                    LicenseType = l.LicenseType.Trim(),
                    SpecialtyId = l.SpecialtyId,
                    Number = ProfessionalOptions.Normalize(l.Number),
                    StateId = l.StateId,
                    Issuer = ProfessionalOptions.Normalize(l.Issuer),
                    IssuedAt = l.IssuedAt,
                    ExpiresAt = l.ExpiresAt,
                    VerificationStatus = string.IsNullOrWhiteSpace(l.VerificationStatus)
                        ? "Pending"
                        : l.VerificationStatus.Trim(),
                    CreatedAt = now,
                }));

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    public async Task UpdateAsync(Employee employee, CancellationToken ct = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            // 1. Scalares HR del empleado (update dirigido, sin tracking).
            await dbContext.Employees
                .Where(x => x.Id == employee.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.FirstName, employee.FirstName)
                    .SetProperty(x => x.MiddleName, employee.MiddleName)
                    .SetProperty(x => x.LastName, employee.LastName)
                    .SetProperty(x => x.Email, employee.Email)
                    .SetProperty(x => x.PhoneCountryCode, employee.PhoneCountryCode)
                    .SetProperty(x => x.PhoneNumber, employee.PhoneNumber)
                    .SetProperty(x => x.JobTitle, employee.JobTitle)
                    .SetProperty(x => x.Department, employee.Department)
                    .SetProperty(x => x.HireDate, employee.HireDate)
                    .SetProperty(x => x.Status, employee.Status)
                    .SetProperty(x => x.UpdatedAt, employee.UpdatedAt), ct);

            // 2. Clínicas: sync total (borrar e insertar).
            await dbContext.EmployeeClinics
                .Where(x => x.EmployeeId == employee.Id)
                .ExecuteDeleteAsync(ct);

            var now = DateTime.UtcNow;
            dbContext.EmployeeClinics.AddRange(
                employee.ClinicAssignments.Select(c => new EmployeeClinic
                {
                    EmployeeId = employee.Id,
                    ClinicId = c.ClinicId,
                    IsPrimary = c.IsPrimary,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt == default ? now : c.CreatedAt,
                }));

            // 3. Extensión profesional: reemplazo completo (borrar = sin extensión).
            var existingProfessionalId = await dbContext.Professionals
                .Where(p => p.EmployeeId == employee.Id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            if (employee.Professional is null)
            {
                if (existingProfessionalId is not null)
                {
                    // El FK en BD es CASCADE: borrar la fila professional elimina
                    // sus especialidades y licencias.
                    await dbContext.Professionals
                        .Where(p => p.Id == existingProfessionalId)
                        .ExecuteDeleteAsync(ct);
                }
            }
            else
            {
                var professionalId = existingProfessionalId ?? employee.Professional.Id;

                if (existingProfessionalId is null)
                {
                    dbContext.Professionals.Add(new Professional
                    {
                        Id = professionalId,
                        EmployeeId = employee.Id,
                        ProfessionalTypeId = employee.Professional.ProfessionalTypeId,
                        Bio = employee.Professional.Bio,
                        OnboardingCompletedAt = employee.Professional.OnboardingCompletedAt,
                        CreatedAt = now,
                        UpdatedAt = employee.Professional.UpdatedAt,
                    });
                }
                else
                {
                    await dbContext.Professionals
                        .Where(p => p.Id == existingProfessionalId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(p => p.ProfessionalTypeId, employee.Professional.ProfessionalTypeId)
                            .SetProperty(p => p.Bio, employee.Professional.Bio)
                            .SetProperty(p => p.OnboardingCompletedAt, employee.Professional.OnboardingCompletedAt)
                            .SetProperty(p => p.UpdatedAt, employee.Professional.UpdatedAt), ct);

                    await dbContext.ProfessionalSpecialties
                        .Where(s => s.ProfessionalId == existingProfessionalId)
                        .ExecuteDeleteAsync(ct);
                    await dbContext.ProfessionalLicenses
                        .Where(l => l.ProfessionalId == existingProfessionalId)
                        .ExecuteDeleteAsync(ct);
                }

                dbContext.ProfessionalSpecialties.AddRange(
                    employee.Professional.Specialties.Select(s => new ProfessionalSpecialty
                    {
                        ProfessionalId = professionalId,
                        SpecialtyId = s.SpecialtyId,
                        IsPrimary = s.IsPrimary,
                        CreatedAt = s.CreatedAt == default ? now : s.CreatedAt,
                    }));

                dbContext.ProfessionalLicenses.AddRange(
                    employee.Professional.Licenses.Select(l => new ProfessionalLicense
                    {
                        Id = l.Id == Guid.Empty ? Guid.NewGuid() : l.Id,
                        ProfessionalId = professionalId,
                        LicenseType = l.LicenseType,
                        SpecialtyId = l.SpecialtyId,
                        Number = l.Number,
                        StateId = l.StateId,
                        Issuer = l.Issuer,
                        IssuedAt = l.IssuedAt,
                        ExpiresAt = l.ExpiresAt,
                        VerificationStatus = l.VerificationStatus,
                        CreatedAt = l.CreatedAt == default ? now : l.CreatedAt,
                    }));
            }

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    public async Task<Professional> AddProfessionalAsync(Professional professional, CancellationToken ct = default)
    {
        dbContext.Professionals.Add(professional);
        await dbContext.SaveChangesAsync(ct);
        return professional;
    }

    public async Task UpdateProfessionalAsync(Professional professional, CancellationToken ct = default)
    {
        await dbContext.Professionals
            .Where(x => x.Id == professional.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProfessionalTypeId, professional.ProfessionalTypeId)
                .SetProperty(x => x.Bio, professional.Bio)
                .SetProperty(x => x.PhotoStorageKey, professional.PhotoStorageKey)
                .SetProperty(x => x.OnboardingCompletedAt, professional.OnboardingCompletedAt)
                .SetProperty(x => x.UpdatedAt, professional.UpdatedAt), ct);
    }

    public async Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken ct = default)
        => await dbContext.Clinics.AnyAsync(x => x.Id == clinicId, ct);

    public async Task<bool> SpecialtyExistsAsync(Guid specialtyId, CancellationToken ct = default)
        => await dbContext.Specialties.AnyAsync(x => x.Id == specialtyId, ct);
}
