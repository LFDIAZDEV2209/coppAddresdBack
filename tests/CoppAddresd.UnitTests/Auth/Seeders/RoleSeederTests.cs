using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Auth.Seeders;

/// <summary>
/// Verifica la convención de escalabilidad del RoleSeeder: rol clínico
/// consolidado Professional, eliminación de roles legado con conversión
/// de holders, roles funcionales por capacidad (Finance/Auditor) y marcado
/// IsSystem idempotente.
/// </summary>
public class RoleSeederTests
{
    private sealed class Harness : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Harness()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlite(_connection)
                .Options;

            Db = new AuthDbContext(options);
            Db.Database.EnsureCreated();
        }

        public AuthDbContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }

    private static async Task SeedAsync(Harness h)
    {
        var logger = NullLogger.Instance;
        await PermissionSeeder.SeedAsync(h.Db, logger);
        await RoleSeeder.SeedAsync(h.Db, logger);
    }

    [Fact]
    public async Task Seed_ProduceRolesConsolidadosYFuncionales()
    {
        using var h = new Harness();
        await SeedAsync(h);

        var names = await h.Db.Roles.AsNoTracking().Select(r => r.Name).ToListAsync();

        Assert.Contains("Professional", names);
        Assert.Contains("Finance", names);
        Assert.Contains("Auditor", names);
        Assert.Contains("CareCoordinator", names);
        // Roles legado eliminados.
        Assert.DoesNotContain("Coordinator", names);
        Assert.DoesNotContain("Physician", names);
        Assert.DoesNotContain("Nutritionist", names);
        Assert.DoesNotContain("Psychologist", names);
    }

    [Fact]
    public async Task Seed_ProfessionalTienePermisosClinicosYNoAdmin()
    {
        using var h = new Harness();
        await SeedAsync(h);

        var permissions = await GetRolePermissionCodesAsync(h, "Professional");

        Assert.Contains("Patients.ViewOwn", permissions);
        Assert.Contains("Patients.Create", permissions);
        Assert.Contains("ClinicalRecords.View", permissions);
        Assert.Contains("Prescriptions.View", permissions);
        // Mínimo privilegio: sin acceso de administración.
        Assert.DoesNotContain("Patients.View", permissions);
        Assert.DoesNotContain("Professionals.View", permissions);
        Assert.DoesNotContain("System.AdminSettings", permissions);
        Assert.DoesNotContain("Finance.View", permissions);
    }

    [Fact]
    public async Task Seed_FinanceSinAccesoClinico()
    {
        using var h = new Harness();
        await SeedAsync(h);

        var permissions = await GetRolePermissionCodesAsync(h, "Finance");

        Assert.Contains("Finance.View", permissions);
        Assert.Contains("Finance.Manage", permissions);
        Assert.Contains("Professionals.View", permissions);
        Assert.DoesNotContain("Patients.ViewOwn", permissions);
        Assert.DoesNotContain("ClinicalRecords.View", permissions);
        Assert.DoesNotContain("Patients.View", permissions);
    }

    [Fact]
    public async Task Seed_AuditorSoloLectura()
    {
        using var h = new Harness();
        await SeedAsync(h);

        var permissions = await GetRolePermissionCodesAsync(h, "Auditor");

        Assert.Contains("Reports.View", permissions);
        Assert.Contains("Patients.View", permissions);
        Assert.Contains("Audit.View", permissions);
        Assert.DoesNotContain("Patients.Create", permissions);
        Assert.DoesNotContain("Patients.Update", permissions);
        Assert.DoesNotContain("Patients.Delete", permissions);
    }

    [Fact]
    public async Task Seed_MarcaRolesDeSistemaComoIsSystem()
    {
        using var h = new Harness();
        await SeedAsync(h);

        var systemRoles = await h
            .Db.Roles.AsNoTracking()
            .Where(r => r.IsSystem)
            .Select(r => r.Name)
            .ToListAsync();

        Assert.Contains("Professional", systemRoles);
        Assert.Contains("Finance", systemRoles);
        Assert.Contains("Auditor", systemRoles);
        Assert.Contains("Nurse", systemRoles);
        Assert.Contains("Receptionist", systemRoles);
        Assert.Contains("CareCoordinator", systemRoles);
        // Roles legado ya no existen (eliminados por el seeder).
        Assert.DoesNotContain("Physician", systemRoles);
        Assert.DoesNotContain("Nutritionist", systemRoles);
        Assert.DoesNotContain("Psychologist", systemRoles);
        Assert.DoesNotContain("Coordinator", systemRoles);
        // El rol Admin lo crea AdminSeeder (IsSystem = true); no corre en este harness.
    }

    [Fact]
    public async Task Seed_EliminaRolesLegado()
    {
        using var h = new Harness();
        await SeedAsync(h);

        // Los 4 roles legado deben haber sido eliminados completamente.
        var names = await h.Db.Roles.AsNoTracking().Select(r => r.Name).ToListAsync();
        Assert.DoesNotContain("Physician", names);
        Assert.DoesNotContain("Nutritionist", names);
        Assert.DoesNotContain("Psychologist", names);
        Assert.DoesNotContain("Coordinator", names);
    }

    [Fact]
    public async Task Seed_ConvierteHoldersLegadosAProfessional()
    {
        using var h = new Harness();

        // Preparar BD existente: crear roles legado y asignar usuarios.
        await PermissionSeeder.SeedAsync(h.Db, NullLogger.Instance);

        // Crear roles manualmente (antes del seed).
        var physicianRole = new ApplicationRole
        {
            Name = "Physician",
            NormalizedName = "PHYSICIAN",
            Description = "Rol legado",
            IsActive = true,
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
        };
        var psychologistRole = new ApplicationRole
        {
            Name = "Psychologist",
            NormalizedName = "PSYCHOLOGIST",
            Description = "Rol legado",
            IsActive = true,
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
        };
        h.Db.Roles.AddRange(physicianRole, psychologistRole);
        await h.Db.SaveChangesAsync();

        // Crear usuario y asignarle Physician.
        var user1 = new ApplicationUser
        {
            UserName = "doctor@test.com",
            Email = "doctor@test.com",
            FirstName = "Dr.",
            LastName = "House",
            IsActive = true,
            EmailConfirmed = true,
        };
        h.Db.Users.Add(user1);
        await h.Db.SaveChangesAsync();

        h.Db.UserRoles.Add(
            new IdentityUserRole<Guid>
            {
                UserId = user1.Id,
                RoleId = physicianRole.Id,
            });
        await h.Db.SaveChangesAsync();

        // Crear otro usuario y asignarle Psychologist.
        var user2 = new ApplicationUser
        {
            UserName = "psi@test.com",
            Email = "psi@test.com",
            FirstName = "Psi",
            LastName = "Logo",
            IsActive = true,
            EmailConfirmed = true,
        };
        h.Db.Users.Add(user2);
        await h.Db.SaveChangesAsync();

        h.Db.UserRoles.Add(
            new IdentityUserRole<Guid>
            {
                UserId = user2.Id,
                RoleId = psychologistRole.Id,
            });
        await h.Db.SaveChangesAsync();

        // Ejecutar el seeder: debe convertir holders a Professional y eliminar Physician/Psychologist.
        await RoleSeeder.SeedAsync(h.Db, NullLogger.Instance);

        // Verificar que Professional existe.
        var professional = await h.Db.Roles.FirstOrDefaultAsync(r => r.Name == "Professional");
        Assert.NotNull(professional);

        // Verificar que los usuarios ahora están en Professional.
        Assert.True(await h.Db.UserRoles.AnyAsync(ur => ur.UserId == user1.Id && ur.RoleId == professional!.Id));
        Assert.True(await h.Db.UserRoles.AnyAsync(ur => ur.UserId == user2.Id && ur.RoleId == professional!.Id));

        // Verificar que los roles legado ya no existen.
        Assert.Null(await h.Db.Roles.FirstOrDefaultAsync(r => r.Name == "Physician"));
        Assert.Null(await h.Db.Roles.FirstOrDefaultAsync(r => r.Name == "Psychologist"));
    }

    [Fact]
    public async Task Seed_ConvierteCoordinatorACareCoordinator()
    {
        using var h = new Harness();

        await PermissionSeeder.SeedAsync(h.Db, NullLogger.Instance);

        // Crear rol Coordinator manualmente.
        var coordinatorRole = new ApplicationRole
        {
            Name = "Coordinator",
            NormalizedName = "COORDINATOR",
            Description = "Alias legado",
            IsActive = true,
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
        };
        h.Db.Roles.Add(coordinatorRole);
        await h.Db.SaveChangesAsync();

        // Crear usuario y asignarle Coordinator.
        var user = new ApplicationUser
        {
            UserName = "coord@test.com",
            Email = "coord@test.com",
            FirstName = "Coord",
            LastName = "Test",
            IsActive = true,
            EmailConfirmed = true,
        };
        h.Db.Users.Add(user);
        await h.Db.SaveChangesAsync();

        h.Db.UserRoles.Add(
            new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = coordinatorRole.Id,
            });
        await h.Db.SaveChangesAsync();

        // Ejecutar el seeder.
        await RoleSeeder.SeedAsync(h.Db, NullLogger.Instance);

        // Verificar que CareCoordinator existe y tiene al usuario.
        var careCoord = await h.Db.Roles.FirstOrDefaultAsync(r => r.Name == "CareCoordinator");
        Assert.NotNull(careCoord);
        Assert.True(await h.Db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == careCoord!.Id));

        // Verificar que Coordinator ya no existe.
        Assert.Null(await h.Db.Roles.FirstOrDefaultAsync(r => r.Name == "Coordinator"));
    }

    [Fact]
    public async Task Seed_LimpiezaEsIdempotente()
    {
        using var h = new Harness();
        await SeedAsync(h);
        await SeedAsync(h);

        var totalRoles = await h.Db.Roles.CountAsync();
        var totalAssignments = await h.Db.RolePermissions.CountAsync();

        Assert.Equal(9, totalRoles); // 9 DefaultRoles (Admin lo crea AdminSeeder, no corre aquí)
        Assert.True(totalAssignments > 0);

        // Los roles legado no deben existir.
        var names = await h.Db.Roles.AsNoTracking().Select(r => r.Name).ToListAsync();
        Assert.DoesNotContain("Physician", names);
        Assert.DoesNotContain("Nutritionist", names);
        Assert.DoesNotContain("Psychologist", names);
        Assert.DoesNotContain("Coordinator", names);
    }

    private static async Task<IReadOnlyList<string>> GetRolePermissionCodesAsync(
        Harness h,
        string roleName
    )
    {
        return await h
            .Db.Roles.AsNoTracking()
            .Where(r => r.Name == roleName)
            .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code))
            .ToListAsync();
    }
}
