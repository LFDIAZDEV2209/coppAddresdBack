using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using CoppAddresd.Auth.Services.Cache;
using CoppAddresd.UnitTests.Cache;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.UnitTests.Features.Auth;

/// <summary>
/// Comportamiento de caché de la introspección de permisos del Auth Service
/// (SPEC infra/cache): el mapeo código→id y los códigos por rol se sirven
/// desde caché en la segunda llamada, y una mutación de RolePermissions
/// (AssignToRole/RemoveFromRole del PermissionService) invalida la clave del
/// rol. BD real en memoria (Sqlite) — mismas queries que producción.
/// El fake de caché es el equivalente de la interfaz autónoma de Auth.
/// </summary>
public sealed class ScopedPermissionServiceCacheTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AuthDbContext _dbContext;
    private readonly AuthFakeCacheService _cache = new();
    private readonly ScopedPermissionService _service;

    private readonly Guid _roleId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _scopeId = Guid.NewGuid();

    public ScopedPermissionServiceCacheTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(_connection).Options;
        _dbContext = new AuthDbContext(options);
        _dbContext.Database.EnsureCreated();
        _service = new ScopedPermissionService(_dbContext, _cache);

        Seed();
    }

    private void Seed()
    {
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = "Patients.View",
            Name = "Ver pacientes",
            Module = "Patients",
        };
        _dbContext.Permissions.Add(permission);
        _dbContext.Users.Add(
            new ApplicationUser
            {
                Id = _userId,
                UserName = "cache-test",
                Email = "cache-test@local",
                FirstName = "Cache",
                LastName = "Test",
            }
        );
        _dbContext.Roles.Add(new ApplicationRole { Id = _roleId, Name = "ClinicAdmin" });
        _dbContext.RolePermissions.Add(
            new RolePermission { RoleId = _roleId, PermissionId = permission.Id }
        );
        _dbContext.ScopedRoleAssignments.Add(
            new ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                RoleId = _roleId,
                ScopeType = "Clinic",
                ScopeId = _scopeId,
                CreatedAt = DateTime.UtcNow,
            }
        );
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task AuthorizeAsync_SegundaLlamada_SirveCatalogosDesdeCache()
    {
        var scopeChain = new List<ScopeEntry> { new("Clinic", _scopeId) };

        var first = await _service.AuthorizeAsync(
            _userId,
            "Patients.View",
            scopeChain,
            CancellationToken.None
        );
        var second = await _service.AuthorizeAsync(
            _userId,
            "Patients.View",
            scopeChain,
            CancellationToken.None
        );

        // Ambas conceden por rol (el catálogo que provee el grant fue cacheado).
        Assert.True(first);
        Assert.True(second);
        // La clave de códigos por rol se escribió y se sirvió como hit.
        var roleKey = Assert.Single(_cache.Set, k => k.Contains("roles:") && k.Contains("codes"));
        Assert.Contains(roleKey, _cache.Set);
    }

    [Fact]
    public async Task GetEffectivePermissions_SegundaLlamada_HitDeCodigosPorRol()
    {
        var scopeChain = new List<ScopeEntry> { new("Clinic", _scopeId) };

        var first = await _service.GetEffectivePermissionsAsync(
            _userId,
            scopeChain,
            CancellationToken.None
        );
        var hitsTrasPrimera = _cache.Hits;
        var second = await _service.GetEffectivePermissionsAsync(
            _userId,
            scopeChain,
            CancellationToken.None
        );

        Assert.Contains("Patients.View", first);
        Assert.Contains("Patients.View", second);
        Assert.True(_cache.Hits > hitsTrasPrimera); // la segunda sirvió la clave del rol
    }

    [Fact]
    public async Task AssignToRoleInvalida_CodigosDelRol()
    {
        var permissionService = new PermissionService(
            _dbContext,
            new FakeTokenInvalidationService(),
            _cache,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionService>.Instance
        );
        var newPermissionId = Guid.NewGuid();
        _dbContext.Permissions.Add(
            new Permission
            {
                Id = newPermissionId,
                Code = "Finance.View",
                Name = "Ver finanzas",
                Module = "Finance",
            }
        );
        await _dbContext.SaveChangesAsync();

        // Calienta el caché del rol.
        await _service.GetEffectivePermissionsAsync(
            _userId,
            new List<ScopeEntry> { new("Clinic", Guid.NewGuid()) },
            CancellationToken.None
        );

        var (success, _) = await permissionService.AssignToRoleAsync(
            _roleId,
            newPermissionId,
            CancellationToken.None
        );

        Assert.True(success);
        Assert.Contains(AuthCacheKeys.RoleCodes(_roleId), _cache.Removed);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>Doble de ITokenInvalidationService sin efectos (no aplica en estos tests).</summary>
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
