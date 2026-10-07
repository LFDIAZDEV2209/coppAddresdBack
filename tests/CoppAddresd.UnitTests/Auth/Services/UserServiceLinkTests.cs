using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Vinculación de un alta a una cuenta existente (POST /api/auth/users/link):
/// unión pura de roles y permisos (nada se quita), cuenta inactiva rechazada
/// y preflight del correo. SQLite in-memory con Identity real.
/// </summary>
public class UserServiceLinkTests
{
    private sealed class FakeTokenInvalidationService : ITokenInvalidationService
    {
        public Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task InvalidateUsersTokensAsync(
            IEnumerable<Guid> userIds,
            CancellationToken ct = default
        ) => Task.CompletedTask;
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public Harness()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddDbContext<AuthDbContext>(options => options.UseSqlite(_connection));
            services
                .AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                })
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<AuthDbContext>()
                .AddDefaultTokenProviders();

            services.AddSingleton<ITokenInvalidationService>(new FakeTokenInvalidationService());

            _provider = services.BuildServiceProvider();
            Db.Database.EnsureCreated();

            Users = new UserService(
                UserManager,
                RoleManager,
                Db,
                _provider.GetRequiredService<ITokenInvalidationService>(),
                NullLogger<UserService>.Instance
            );
        }

        public AuthDbContext Db => _provider.GetRequiredService<AuthDbContext>();
        public UserService Users { get; }
        public UserManager<ApplicationUser> UserManager =>
            _provider.GetRequiredService<UserManager<ApplicationUser>>();
        public RoleManager<ApplicationRole> RoleManager =>
            _provider.GetRequiredService<RoleManager<ApplicationRole>>();

        public async Task<ApplicationUser> CreateUserAsync(
            string email,
            bool isActive = true,
            string? password = "Test1234!"
        )
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                IsActive = isActive,
                EmailConfirmed = true,
            };
            var result = password is null
                ? await UserManager.CreateAsync(user)
                : await UserManager.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
            return user;
        }

        public async Task<ApplicationRole> CreateRoleAsync(string name)
        {
            var role = new ApplicationRole { Name = name };
            var result = await RoleManager.CreateAsync(role);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
            return role;
        }

        public async Task<Permission> CreatePermissionAsync(string code)
        {
            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = code,
                Module = "Test",
            };
            Db.Permissions.Add(permission);
            await Db.SaveChangesAsync();
            return permission;
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }

    [Fact]
    public async Task LinkAsync_CuentaInexistente_DevuelveNotFound()
    {
        using var h = new Harness();

        var (success, error, user, notFound) = await h.Users.LinkAsync(
            new LinkUserAccountRequest { Email = "nadie@test.com" }
        );

        Assert.False(success);
        Assert.True(notFound);
        Assert.Null(user);
        Assert.Equal("Usuario no encontrado", error);
    }

    [Fact]
    public async Task LinkAsync_CuentaInactiva_DevuelveConflicto()
    {
        using var h = new Harness();
        await h.CreateUserAsync("inactivo@test.com", isActive: false);

        var (success, error, user, notFound) = await h.Users.LinkAsync(
            new LinkUserAccountRequest { Email = "inactivo@test.com" }
        );

        Assert.False(success);
        Assert.False(notFound);
        Assert.Null(user);
        Assert.Contains("inactiva", error!);
    }

    [Fact]
    public async Task LinkAsync_CuentaActiva_SumaRolesYPermisosSinQuitarLosExistentes()
    {
        using var h = new Harness();
        var user = await h.CreateUserAsync("vincular@test.com");

        var existingRole = await h.CreateRoleAsync("RolExistente");
        var newRole = await h.CreateRoleAsync("RolNuevo");
        await h.UserManager.AddToRoleAsync(user, existingRole.Name!);

        var existingPermission = await h.CreatePermissionAsync("Test.PermisoExistente");
        var newPermission = await h.CreatePermissionAsync("Test.PermisoNuevo");
        h.Db.UserPermissions.Add(
            new UserPermission { UserId = user.Id, PermissionId = existingPermission.Id }
        );
        await h.Db.SaveChangesAsync();

        var (success, error, linked, notFound) = await h.Users.LinkAsync(
            new LinkUserAccountRequest
            {
                Email = "vincular@test.com",
                RoleIds = [newRole.Id],
                PermissionIds = [newPermission.Id],
            }
        );

        Assert.True(success, error);
        Assert.False(notFound);
        Assert.NotNull(linked);

        var roles = await h.UserManager.GetRolesAsync(user);
        Assert.Contains("RolExistente", roles);
        Assert.Contains("RolNuevo", roles);

        var permissionIds = await h.Db.UserPermissions
            .Where(up => up.UserId == user.Id)
            .Select(up => up.PermissionId)
            .ToListAsync();
        Assert.Contains(existingPermission.Id, permissionIds);
        Assert.Contains(newPermission.Id, permissionIds);
    }

    [Fact]
    public async Task GetEmailAvailabilityAsync_CuentaExistente_DevuelveContexto()
    {
        using var h = new Harness();
        var user = await h.CreateUserAsync("cuenta@test.com");

        var result = await h.Users.GetEmailAvailabilityAsync("  cuenta@test.com ");

        Assert.True(result.Exists);
        Assert.True(result.IsActive);
        Assert.True(result.HasPassword);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("Test", result.FirstName);
    }

    [Fact]
    public async Task GetEmailAvailabilityAsync_CorreoLibre_DevuelveSinCuenta()
    {
        using var h = new Harness();

        var result = await h.Users.GetEmailAvailabilityAsync("libre@test.com");

        Assert.False(result.Exists);
        Assert.False(result.IsActive);
        Assert.False(result.HasPassword);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task GetEmailAvailabilityAsync_EntradaVacia_DevuelveSinCuenta()
    {
        using var h = new Harness();

        var result = await h.Users.GetEmailAvailabilityAsync("   ");

        Assert.False(result.Exists);
        Assert.Null(result.UserId);
    }
}
