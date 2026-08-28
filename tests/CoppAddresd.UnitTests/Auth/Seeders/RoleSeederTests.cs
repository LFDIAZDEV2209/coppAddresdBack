using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Seeders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Auth.Seeders;

/// <summary>
/// Verifica la convención de escalabilidad del RoleSeeder: rol clínico
/// consolidado Professional, aliases legado con permisos idénticos, roles
/// funcionales por capacidad (Finance/Auditor/Coordinator) y marcado IsSystem
/// idempotente.
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
        Assert.Contains("Coordinator", names);
        // Aliases legado: existen (conservan usuarios ya asignados).
        Assert.Contains("Physician", names);
        Assert.Contains("Nutritionist", names);
        Assert.Contains("Psychologist", names);
    }

    [Fact]
    public async Task Seed_LegadosNoRecibenPermisosPorDefecto()
    {
        using var h = new Harness();
        await SeedAsync(h);

        // En BD nueva los aliases legado existen pero sin asignaciones por
        // defecto: los permisos los conservan solo usuarios ya asignados en
        // BD existentes (ver Seed_NoRevocaPermisosDeAliasesLegadoExistentes).
        foreach (var legacy in new[] { "Physician", "Nutritionist", "Psychologist" })
        {
            var legacyPermissions = await GetRolePermissionCodesAsync(h, legacy);
            Assert.Empty(legacyPermissions);
        }
    }

    [Fact]
    public async Task Seed_NoRevocaPermisosDeAliasesLegadoExistentes()
    {
        using var h = new Harness();
        await SeedAsync(h);

        // Simula una BD existente: el legado ya tenía los permisos clínicos
        // compartidos (los de Professional). El seed no debe quitárselos.
        var professionalPermissions = await GetRolePermissionCodesAsync(h, "Professional");
        var physician = await h.Db.Roles.SingleAsync(r => r.Name == "Physician");
        h.Db.RolePermissions.AddRange(
            professionalPermissions.Select(code =>
            {
                var permission = h.Db.Permissions.Single(p => p.Code == code);
                return new CoppAddresd.Auth.Entities.RolePermission
                {
                    RoleId = physician.Id,
                    PermissionId = permission.Id,
                };
            })
        );
        await h.Db.SaveChangesAsync();

        // Re-sembrar: los permisos existentes del alias se conservan.
        await SeedAsync(h);

        var preserved = await GetRolePermissionCodesAsync(h, "Physician");
        Assert.Equal(professionalPermissions.OrderBy(c => c), preserved.OrderBy(c => c));
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
        Assert.Contains("Coordinator", systemRoles);
        Assert.Contains("Physician", systemRoles);
        Assert.Contains("Nutritionist", systemRoles);
        Assert.Contains("Psychologist", systemRoles);
        Assert.Contains("Nurse", systemRoles);
        Assert.Contains("Receptionist", systemRoles);
        Assert.Contains("CareCoordinator", systemRoles);
        // El rol Admin lo crea AdminSeeder (IsSystem = true); no corre en este harness.
    }

    [Fact]
    public async Task Seed_EsIdempotente()
    {
        using var h = new Harness();
        await SeedAsync(h);
        await SeedAsync(h);

        var totalRoles = await h.Db.Roles.CountAsync();
        var totalAssignments = await h.Db.RolePermissions.CountAsync();

        Assert.Equal(13, totalRoles); // 10 DefaultRoles + 3 aliases legado (Admin lo crea AdminSeeder, no corre aquí)
        Assert.True(totalAssignments > 0);
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
