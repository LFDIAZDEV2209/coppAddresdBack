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
/// Protección de roles de sistema: un rol IsSystem no se elimina ni se
/// renombra/desactiva sin System.AdminSettings (allowSystemChanges).
/// </summary>
public class RoleServiceTests
{
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

            services.AddScoped<RoleService>();
            services.AddSingleton<ITokenInvalidationService>(new FakeTokenInvalidationService());

            _provider = services.BuildServiceProvider();
            Db.Database.EnsureCreated();
        }

        /// <summary>Fake no-op: la invalidación de tokens no interesa en estos tests.</summary>
        private sealed class FakeTokenInvalidationService : ITokenInvalidationService
        {
            public Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default) =>
                Task.CompletedTask;

            public Task InvalidateUsersTokensAsync(
                IEnumerable<Guid> userIds,
                CancellationToken ct = default
            ) => Task.CompletedTask;
        }

        public AuthDbContext Db => _provider.GetRequiredService<AuthDbContext>();
        public RoleService Roles => _provider.GetRequiredService<RoleService>();
        public UserManager<ApplicationUser> Users =>
            _provider.GetRequiredService<UserManager<ApplicationUser>>();

        public async Task<Guid> CreateRoleAsync(string name, bool isSystem)
        {
            var role = new ApplicationRole
            {
                Name = name,
                Description = "test",
                IsActive = true,
                IsSystem = isSystem,
            };
            Db.Roles.Add(role);
            await Db.SaveChangesAsync();
            return role.Id;
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }

    [Fact]
    public async Task Delete_RolIsSystemSinPermiso_FallaYPersiste()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("SystemRole", isSystem: true);

        var (success, error) = await h.Roles.DeleteAsync(roleId);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.True(await h.Db.Roles.AnyAsync(r => r.Id == roleId));
    }

    [Fact]
    public async Task Delete_RolIsSystemConPermiso_Elimina()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("SystemRole", isSystem: true);

        var (success, _) = await h.Roles.DeleteAsync(roleId, allowSystemChanges: true);

        Assert.True(success);
        Assert.False(await h.Db.Roles.AnyAsync(r => r.Id == roleId));
    }

    [Fact]
    public async Task Delete_RolNoIsSystem_EliminaSinPermisoEspecial()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("CustomRole", isSystem: false);

        var (success, _) = await h.Roles.DeleteAsync(roleId);

        Assert.True(success);
    }

    [Fact]
    public async Task Update_RenombrarRolIsSystemSinPermiso_Falla()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("SystemRole", isSystem: true);

        var (success, error) = await h.Roles.UpdateAsync(
            roleId,
            new UpdateRoleRequest { Name = "Renamed" }
        );

        Assert.False(success);
        Assert.NotNull(error);
        Assert.True(await h.Db.Roles.AnyAsync(r => r.Id == roleId && r.Name == "SystemRole"));
    }

    [Fact]
    public async Task Update_DesactivarRolIsSystemSinPermiso_Falla()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("SystemRole", isSystem: true);

        var (success, _) = await h.Roles.UpdateAsync(
            roleId,
            new UpdateRoleRequest { IsActive = false }
        );

        Assert.False(success);
        Assert.True(await h.Db.Roles.AnyAsync(r => r.Id == roleId && r.IsActive));
    }

    [Fact]
    public async Task Update_DescripcionDeRolIsSystem_EditableSinPermisoEspecial()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("SystemRole", isSystem: true);

        var (success, _) = await h.Roles.UpdateAsync(
            roleId,
            new UpdateRoleRequest { Description = "nueva descripción" }
        );

        Assert.True(success);
        Assert.Equal(
            "nueva descripción",
            (await h.Db.Roles.SingleAsync(r => r.Id == roleId)).Description
        );
    }

    [Fact]
    public async Task Update_RenombrarRolIsSystemConPermiso_Cambia()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("SystemRole", isSystem: true);

        var (success, _) = await h.Roles.UpdateAsync(
            roleId,
            new UpdateRoleRequest { Name = "Renamed" },
            allowSystemChanges: true
        );

        Assert.True(success);
        Assert.True(await h.Db.Roles.AnyAsync(r => r.Id == roleId && r.Name == "Renamed"));
    }
}
