using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Services;
using CoppAddresd.UnitTests.Cache;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Features.Auth;

/// <summary>
/// Split de seguridad entre permisos estrictos (claims JWT) y efectivos (UI):
/// <see cref="PermissionService.GetUserAllPermissionCodesAsync"/> NUNCA incluye
/// asignaciones con scope (un permiso de clínica en el JWT sería global =
/// escalada), mientras <see cref="PermissionService.GetUserEffectivePermissionCodesAsync"/>
/// une directos + roles globales + roles con scope para el gating del frontend
/// (<c>/api/auth/me</c>). Sin este union, un profesional solo con rol scoped ve
/// menús vacíos aunque el backend sí lo autoriza por introspección.
/// </summary>
public sealed class EffectivePermissionCodesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AuthDbContext _db;
    private readonly PermissionService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _globalRoleId = Guid.NewGuid();
    private readonly Guid _clinicRoleId = Guid.NewGuid();
    private readonly Guid _clinicId = Guid.NewGuid();

    public EffectivePermissionCodesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(_connection).Options;
        _db = new AuthDbContext(options);
        _db.Database.EnsureCreated();
        _service = new PermissionService(
            _db,
            new FakeTokenInvalidationService(),
            new AuthFakeCacheService(),
            NullLogger<PermissionService>.Instance
        );
        Seed();
    }

    private void Seed()
    {
        var globalPerm = new Permission
        {
            Id = Guid.NewGuid(),
            Code = "Patients.View",
            Name = "Ver",
            Module = "Patients",
        };
        var clinicPerm = new Permission
        {
            Id = Guid.NewGuid(),
            Code = "Patients.Create",
            Name = "Crear",
            Module = "Patients",
        };
        _db.Permissions.AddRange(globalPerm, clinicPerm);
        _db.Users.Add(
            new ApplicationUser
            {
                Id = _userId,
                UserName = "scoped-only",
                Email = "scoped@local",
                FirstName = "Scoped",
                LastName = "Only",
            }
        );
        _db.Roles.AddRange(
            new ApplicationRole { Id = _globalRoleId, Name = "Professional" },
            new ApplicationRole { Id = _clinicRoleId, Name = "ClinicPro" }
        );
        _db.RolePermissions.AddRange(
            new RolePermission { RoleId = _globalRoleId, PermissionId = globalPerm.Id },
            new RolePermission { RoleId = _clinicRoleId, PermissionId = clinicPerm.Id }
        );
        // Solo asignaciones con scope: Global + Clinic. Cero roles/permits directos.
        _db.ScopedRoleAssignments.AddRange(
            new ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                RoleId = _globalRoleId,
                ScopeType = "Global",
                ScopeId = null,
                CreatedAt = DateTime.UtcNow,
            },
            new ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                RoleId = _clinicRoleId,
                ScopeType = "Clinic",
                ScopeId = _clinicId,
                CreatedAt = DateTime.UtcNow,
            }
        );
        _db.SaveChanges();
    }

    [Fact]
    public async Task Strict_ExcluyeAsignacionesConScope()
    {
        var codes = await _service.GetUserAllPermissionCodesAsync(_userId);

        // Estricto: vacío para usuario solo-scoped (así los claims JWT no
        // globalizan permisos de clínica). NO cambiar sin auditoría de seguridad.
        Assert.Empty(codes);
    }

    [Fact]
    public async Task Effective_IncluyeRolesGlobalYClinica()
    {
        var codes = (await _service.GetUserEffectivePermissionCodesAsync(_userId)).ToList();

        Assert.Contains("Patients.View", codes); // rol scope Global
        Assert.Contains("Patients.Create", codes); // rol scope Clinic
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeTokenInvalidationService : ITokenInvalidationService
    {
        public Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task InvalidateUsersTokensAsync(
            IEnumerable<Guid> userIds,
            CancellationToken ct = default
        ) => Task.CompletedTask;
    }
}
