using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Tests de roles scoped en UserService: GetAllAsync y GetByIdAsync incluyen
/// asignaciones de rol con scope (clínica/organización) en la respuesta.
/// Usa SQLite in-memory (mismo patrón que BulkCreateUsersTests) con Identity
/// real y un UserService de prueba que overridea los métodos de acceso a
/// datos scoped (SQLite no soporta ANY(@ids) ni el esquema erp).
/// </summary>
public class UserServiceScopedRolesTests
{
    /// <summary>
    /// Servicio de prueba que reemplaza la carga de roles scoped por una
    /// implementación compatible con SQLite (EF LINQ en vez de raw SQL)
    /// y la resolución de nombres de scope por un stub.
    /// </summary>
    private sealed class TestUserService : UserService
    {
        private readonly AuthDbContext _testDbContext;

        public TestUserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            AuthDbContext dbContext,
            ITokenInvalidationService tokenInvalidation)
            : base(userManager, roleManager, dbContext, tokenInvalidation, NullLogger<UserService>.Instance)
        {
            _testDbContext = dbContext;
        }

        protected override async Task<Dictionary<Guid, List<UserScopedRoleResponse>>> GetScopedRolesForUsersAsync(
            Guid[] userIds,
            CancellationToken ct)
        {
            if (userIds.Length == 0)
                return new Dictionary<Guid, List<UserScopedRoleResponse>>();

            // Implementación compatible con SQLite: query directa sobre
            // ScopedRoleAssignments + Roles.
            var assignments = await _testDbContext.ScopedRoleAssignments
                .Where(a => userIds.Contains(a.UserId))
                .Select(a => new
                {
                    a.UserId,
                    RoleName = a.Role.Name!,
                    a.ScopeType,
                    a.ScopeId,
                })
                .ToListAsync(ct);

            if (assignments.Count == 0)
                return new Dictionary<Guid, List<UserScopedRoleResponse>>();

            var result = new Dictionary<Guid, List<UserScopedRoleResponse>>();
            foreach (var a in assignments)
            {
                var item = new UserScopedRoleResponse(
                    RoleName: a.RoleName,
                    ScopeType: a.ScopeType,
                    ScopeName: null); // SQLite no tiene erp.clinics/organizations.

                if (!result.TryGetValue(a.UserId, out var list))
                {
                    list = new List<UserScopedRoleResponse>();
                    result[a.UserId] = list;
                }
                list.Add(item);
            }

            return result;
        }

        protected override Task<Dictionary<Guid, string>> ResolveScopeNamesAsync(
            Guid[] clinicIds,
            Guid[] orgIds,
            CancellationToken ct)
            => Task.FromResult(new Dictionary<Guid, string>());
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

            // Usar TestUserService en lugar de UserService real.
            Users = new TestUserService(
                UserManager,
                RoleManager,
                Db,
                _provider.GetRequiredService<ITokenInvalidationService>());
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

        public AuthDbContext Db => _provider.GetRequiredService<AuthDbContext>();
        public TestUserService Users { get; }
        public UserManager<ApplicationUser> UserManager =>
            _provider.GetRequiredService<UserManager<ApplicationUser>>();
        public RoleManager<ApplicationRole> RoleManager =>
            _provider.GetRequiredService<RoleManager<ApplicationRole>>();

        public async Task<Guid> CreateRoleAsync(string name)
        {
            var role = new ApplicationRole
            {
                Name = name,
                Description = "test",
                IsActive = true,
                IsSystem = false,
            };
            await RoleManager.CreateAsync(role);
            return role.Id;
        }

        public async Task<ApplicationUser> CreateUserAsync(string firstName, string lastName, string email)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                EmailConfirmed = true,
            };
            await UserManager.CreateAsync(user);
            return user;
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }

    [Fact]
    public async Task GetAllAsync_SinRolesScoped_SinCampoScopedRoles()
    {
        using var h = new Harness();
        await h.CreateRoleAsync("Admin");
        var user = await h.CreateUserAsync("Ana", "García", "ana@test.com");
        await h.UserManager.AddToRoleAsync(user, "Admin");

        var result = await h.Users.GetAllAsync();
        var list = result.ToList();

        Assert.Single(list);
        Assert.Equal(["Admin"], list[0].Roles);
        Assert.Null(list[0].ScopedRoles);
    }

    [Fact]
    public async Task GetAllAsync_ConRolScoped_IncluyeScopedRoles()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("Professional");
        var clinicId = Guid.NewGuid();
        var user = await h.CreateUserAsync("Luis", "Pérez", "luis@test.com");

        // Insertar asignación scoped directamente en la DB.
        h.Db.ScopedRoleAssignments.Add(new ScopedRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = roleId,
            ScopeType = "Clinic",
            ScopeId = clinicId,
        });
        await h.Db.SaveChangesAsync();

        var result = await h.Users.GetAllAsync();
        var list = result.ToList();

        Assert.Single(list);
        Assert.Empty(list[0].Roles); // Sin roles globales.
        Assert.NotNull(list[0].ScopedRoles);
        Assert.Single(list[0].ScopedRoles!);
        Assert.Equal("Professional", list[0].ScopedRoles![0].RoleName);
        Assert.Equal("Clinic", list[0].ScopedRoles![0].ScopeType);
    }

    [Fact]
    public async Task GetAllAsync_VariosUsuarios_RolesScopedBatch()
    {
        using var h = new Harness();
        var roleId1 = await h.CreateRoleAsync("Professional");
        var roleId2 = await h.CreateRoleAsync("Nurse");
        var clinicId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var user1 = await h.CreateUserAsync("Ana", "García", "ana@test.com");
        var user2 = await h.CreateUserAsync("Luis", "Pérez", "luis@test.com");

        h.Db.ScopedRoleAssignments.AddRange(
            new ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user1.Id,
                RoleId = roleId1,
                ScopeType = "Clinic",
                ScopeId = clinicId,
            },
            new ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user2.Id,
                RoleId = roleId2,
                ScopeType = "Organization",
                ScopeId = orgId,
            });
        await h.Db.SaveChangesAsync();

        var result = await h.Users.GetAllAsync();
        var list = result.ToList();

        Assert.Equal(2, list.Count);

        var scoped1 = list.First(u => u.Email == "ana@test.com").ScopedRoles;
        Assert.NotNull(scoped1);
        Assert.Single(scoped1!);
        Assert.Equal("Professional", scoped1![0].RoleName);
        Assert.Equal("Clinic", scoped1[0].ScopeType);

        var scoped2 = list.First(u => u.Email == "luis@test.com").ScopedRoles;
        Assert.NotNull(scoped2);
        Assert.Single(scoped2!);
        Assert.Equal("Nurse", scoped2![0].RoleName);
        Assert.Equal("Organization", scoped2[0].ScopeType);
    }

    [Fact]
    public async Task GetByIdAsync_ConRolScoped_IncluyeScopedRoles()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("Professional");
        var clinicId = Guid.NewGuid();
        var user = await h.CreateUserAsync("María", "López", "maria@test.com");

        h.Db.ScopedRoleAssignments.Add(new ScopedRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = roleId,
            ScopeType = "Clinic",
            ScopeId = clinicId,
        });
        await h.Db.SaveChangesAsync();

        var result = await h.Users.GetByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Empty(result!.Roles);
        Assert.NotNull(result.ScopedRoles);
        Assert.Single(result.ScopedRoles!);
        Assert.Equal("Professional", result.ScopedRoles![0].RoleName);
        Assert.Equal("Clinic", result.ScopedRoles![0].ScopeType);
    }

    [Fact]
    public async Task GetByIdAsync_SinRolesScoped_SinCampoScopedRoles()
    {
        using var h = new Harness();
        var user = await h.CreateUserAsync("Carlos", "Ruiz", "carlos@test.com");

        var result = await h.Users.GetByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Null(result!.ScopedRoles);
    }

    [Fact]
    public async Task GetByIdAsync_UsuarioInexistente_RetornaNull()
    {
        using var h = new Harness();
        var result = await h.Users.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_MixtoRolesGlobalesYScoped_TodosPresentes()
    {
        using var h = new Harness();
        var adminRoleId = await h.CreateRoleAsync("Admin");
        var scopedRoleId = await h.CreateRoleAsync("Professional");
        var clinicId = Guid.NewGuid();
        var user = await h.CreateUserAsync("Pedro", "Gómez", "pedro@test.com");

        // Rol global + scoped.
        await h.UserManager.AddToRoleAsync(user, "Admin");
        h.Db.ScopedRoleAssignments.Add(new ScopedRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = scopedRoleId,
            ScopeType = "Clinic",
            ScopeId = clinicId,
        });
        await h.Db.SaveChangesAsync();

        var result = await h.Users.GetAllAsync();
        var userResult = result.First();

        Assert.Equal(["Admin"], userResult.Roles);
        Assert.NotNull(userResult.ScopedRoles);
        Assert.Single(userResult.ScopedRoles!);
        Assert.Equal("Professional", userResult.ScopedRoles![0].RoleName);
        Assert.Equal("Clinic", userResult.ScopedRoles![0].ScopeType);
    }

    [Fact]
    public async Task GetAllAsync_MultiplesScopedRolesEnUnUsuario_TodosIncluidos()
    {
        using var h = new Harness();
        var roleId1 = await h.CreateRoleAsync("Professional");
        var roleId2 = await h.CreateRoleAsync("Nurse");
        var clinicId = Guid.NewGuid();
        var user = await h.CreateUserAsync("Sofia", "Torres", "sofia@test.com");

        h.Db.ScopedRoleAssignments.AddRange(
            new ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = roleId1,
                ScopeType = "Clinic",
                ScopeId = clinicId,
            },
            new ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = roleId2,
                ScopeType = "Clinic",
                ScopeId = clinicId,
            });
        await h.Db.SaveChangesAsync();

        var result = await h.Users.GetAllAsync();
        var userResult = result.First();

        Assert.NotNull(userResult.ScopedRoles);
        Assert.Equal(2, userResult.ScopedRoles!.Length);
        Assert.Contains(userResult.ScopedRoles, r => r.RoleName == "Professional");
        Assert.Contains(userResult.ScopedRoles, r => r.RoleName == "Nurse");
    }

    [Fact]
    public async Task Response_FormatoEscalable_RecordSinBreakingChanges()
    {
        // Verificar que UserResponse sigue siendo un record con la shape
        // existente (positional parameters) y el nuevo campo optional.
        var response = new UserResponse(
            Id: "test-id",
            Email: "test@test.com",
            FirstName: "Test",
            LastName: "User",
            IsActive: true,
            CreatedAt: DateTime.UtcNow,
            Roles: ["Admin"],
            ScopedRoles: null);

        Assert.Equal("test-id", response.Id);
        Assert.Equal(["Admin"], response.Roles);
        Assert.Null(response.ScopedRoles);

        var withScoped = response with
        {
            ScopedRoles = [new("Professional", "Clinic", "Clínica Central")]
        };
        Assert.NotNull(withScoped.ScopedRoles);
        Assert.Single(withScoped.ScopedRoles!);
        Assert.Equal("Clínica Central", withScoped.ScopedRoles![0].ScopeName);
    }
}
