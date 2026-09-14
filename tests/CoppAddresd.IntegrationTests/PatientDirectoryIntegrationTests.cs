using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Pruebas de integración del directorio de pacientes (stats scoped y
/// profesionales en el listado) con PostgreSQL real. Requieren la variable
/// COP_TEST_DB_CONNECTION; si no está definida, los tests se saltan.
/// Todo el fixture vive en una transacción que se revierte al final: nunca
/// contamina la BD de desarrollo.
/// </summary>
public sealed class PatientDirectoryIntegrationTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private bool _skipped;
    private AppDbContext _db = null!;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction = null!;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _skipped = true;
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        _db = new AppDbContext(options);
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_skipped)
        {
            return;
        }

        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    private bool IsSkipped => _skipped;

    private static PatientProfile NewPatient(
        Guid clinicId,
        string firstName,
        string status,
        DateTime createdAt
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = "Prueba",
            Status = status,
            ClinicId = clinicId,
            CreatedAt = createdAt,
        };

    private async Task<Guid> SeedOrganizationAndClinicAsync()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = $"ORG-{Guid.NewGuid():N}"[..16],
            Name = $"Org {Guid.NewGuid():N}",
        };
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync();

        var clinic = new Clinic
        {
            Id = Guid.NewGuid(),
            Name = $"Clínica {Guid.NewGuid():N}",
            OrganizationId = organization.Id,
        };
        _db.Clinics.Add(clinic);
        await _db.SaveChangesAsync();
        return clinic.Id;
    }

    /// <summary>Empleado con extensión clínica (erp.employees + erp.professionals).</summary>
    private async Task<Guid> SeedProfessionalAsync(Guid organizationId, string fullName)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FirstName = fullName,
            MiddleName = "",
            LastName = "Prueba",
            Email = $"prof.{Guid.NewGuid():N}@test.local",
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var professional = new Professional { Id = Guid.NewGuid(), EmployeeId = employee.Id };
        _db.Professionals.Add(professional);
        await _db.SaveChangesAsync();
        return professional.Id;
    }

    [Fact]
    public async Task Stats_ConClinicaActiva_CalculaTotalActivosNuevosYSinAsignar()
    {
        if (IsSkipped)
        {
            return;
        }

        var clinicId = await SeedOrganizationAndClinicAsync();
        var monthStart = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc
        );

        _db.PatientProfiles.AddRange(
            NewPatient(clinicId, "Ana", "Activo", DateTime.UtcNow.AddDays(-2)),
            NewPatient(clinicId, "Luis", "Activo", DateTime.UtcNow.AddMonths(-3)),
            NewPatient(clinicId, "Sara", "Inactivo", DateTime.UtcNow.AddDays(-1))
        );
        await _db.SaveChangesAsync();

        var repository = new PatientRepository(_db);

        var stats = await repository.GetStatsAsync(
            clinicId,
            null,
            monthStart,
            CancellationToken.None
        );

        Assert.Equal(3, stats.Total);
        Assert.Equal(2, stats.Active);
        Assert.Equal(2, stats.NewThisMonth);
        Assert.Equal(3, stats.WithoutProfessional);
    }

    [Fact]
    public async Task Stats_ConAlcancePropio_SoloPacientesAsignadosAlProfesional()
    {
        if (IsSkipped)
        {
            return;
        }

        var clinicId = await SeedOrganizationAndClinicAsync();
        var professionalId = await SeedProfessionalAsync(
            (await _db.Organizations.FirstAsync()).Id,
            "Dra. Ana Ríos"
        );

        var ownPatient = NewPatient(clinicId, "PacienteA", "Activo", DateTime.UtcNow);
        var otherPatient = NewPatient(clinicId, "PacienteB", "Activo", DateTime.UtcNow);
        _db.PatientProfiles.AddRange(ownPatient, otherPatient);
        await _db.SaveChangesAsync();

        _db.PatientProfessionalAssignments.Add(
            new PatientProfessionalAssignment
            {
                PatientId = ownPatient.Id,
                ProfessionalId = professionalId,
                ClinicId = clinicId,
                RelationshipType = "Assigned",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
            }
        );
        await _db.SaveChangesAsync();

        var repository = new PatientRepository(_db);
        var monthStart = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc
        );

        var stats = await repository.GetStatsAsync(
            clinicId,
            professionalId,
            monthStart,
            CancellationToken.None
        );

        Assert.Equal(1, stats.Total);
        Assert.Equal(1, stats.Active);
        Assert.Equal(0, stats.WithoutProfessional);
    }

    [Fact]
    public async Task Listado_IncluyeAsignacionesYResuelveNombresSinN1()
    {
        if (IsSkipped)
        {
            return;
        }

        var clinicId = await SeedOrganizationAndClinicAsync();
        var organizationId = (await _db.Organizations.FirstAsync()).Id;
        var professionalId = await SeedProfessionalAsync(organizationId, "Dr. Carlos Mora");

        var assigned = NewPatient(clinicId, "Paciente1", "Activo", DateTime.UtcNow);
        var unassigned = NewPatient(clinicId, "Paciente2", "Activo", DateTime.UtcNow);
        _db.PatientProfiles.AddRange(assigned, unassigned);
        await _db.SaveChangesAsync();

        _db.PatientProfessionalAssignments.Add(
            new PatientProfessionalAssignment
            {
                PatientId = assigned.Id,
                ProfessionalId = professionalId,
                ClinicId = clinicId,
                RelationshipType = "Assigned",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
            }
        );
        await _db.SaveChangesAsync();

        var repository = new PatientRepository(_db);

        var (items, total) = await repository.ListAsync(
            1,
            100,
            null,
            null,
            null,
            clinicId,
            null,
            null,
            null,
            null,
            CancellationToken.None
        );

        Assert.Equal(2, total);
        var assignedItem = items.Single(p => p.Id == assigned.Id);
        var activeAssignments = assignedItem.Assignments.Where(a => a.Status == "Active").ToList();
        Assert.Single(activeAssignments);
        Assert.Equal(professionalId, activeAssignments[0].ProfessionalId);

        var names = await repository.GetProfessionalNamesAsync(
            activeAssignments.Select(a => a.ProfessionalId).ToList(),
            CancellationToken.None
        );

        Assert.Equal("Dr. Carlos Mora Prueba", names[professionalId]);
        Assert.Empty(unassigned.Assignments);
    }
}
