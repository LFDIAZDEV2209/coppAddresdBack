using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Eliminación de usuarios (DELETE /api/auth/users/{id}): desvincula los
/// perfiles de directorio antes del hard delete y devuelve conflicto cuando
/// existe autoría clínica. SQLite in-memory con Identity real; los accesos
/// cross-schema (app/erp) se sustituyen en TestUserService.
/// </summary>
public class UserServiceDeleteTests
{
    /// <summary>
    /// Reemplaza los accesos cross-schema (app.patient_profiles, erp.employees,
    /// app.clinical_baselines) que SQLite no puede resolver, y registra la
    /// desvinculación para poder verificarla.
    /// </summary>
    private sealed class TestUserService : UserService
    {
        public bool ClinicalAuthorship { get; set; }
        public List<Guid> Unlinked { get; } = [];

        public TestUserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            AuthDbContext dbContext,
            ITokenInvalidationService tokenInvalidation
        )
            : base(
                userManager,
                roleManager,
                dbContext,
                tokenInvalidation,
                NullLogger<UserService>.Instance
            ) { }

        protected override Task UnlinkDirectoryProfilesAsync(Guid userId, CancellationToken ct)
        {
            Unlinked.Add(userId);
            return Task.CompletedTask;
        }

        protected override Task<bool> HasClinicalAuthorshipAsync(Guid userId, CancellationToken ct)
            => Task.FromResult(ClinicalAuthorship);
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

            Users = new TestUserService(
                UserManager,
                RoleManager,
                Db,
                _provider.GetRequiredService<ITokenInvalidationService>()
            );
        }

        public AuthDbContext Db => _provider.GetRequiredService<AuthDbContext>();
        public TestUserService Users { get; }
        public UserManager<ApplicationUser> UserManager =>
            _provider.GetRequiredService<UserManager<ApplicationUser>>();
        public RoleManager<ApplicationRole> RoleManager =>
            _provider.GetRequiredService<RoleManager<ApplicationRole>>();

        public async Task<ApplicationUser> CreateUserAsync(string email)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
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
    public async Task DeleteAsync_UsuarioInexistente_DevuelveNotFound()
    {
        using var h = new Harness();

        var (success, error, notFound) = await h.Users.DeleteAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.True(notFound);
        Assert.Equal("Usuario no encontrado", error);
    }

    [Fact]
    public async Task DeleteAsync_UsuarioConPerfil_DesvinculaYELiminaLaCuenta()
    {
        using var h = new Harness();
        var user = await h.CreateUserAsync("borrar@test.com");
        h.Db.ErpAccessOperations.Add(
            new ErpAccessOperation
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EmployeeId = Guid.NewGuid(),
                Status = "Inactive",
                SessionVersion = 1,
            }
        );
        await h.Db.SaveChangesAsync();

        var (success, error, notFound) = await h.Users.DeleteAsync(user.Id);

        Assert.True(success, error);
        Assert.False(notFound);
        Assert.Contains(user.Id, h.Users.Unlinked);
        Assert.Empty(await h.Db.Users.ToListAsync());
        Assert.Empty(await h.Db.ErpAccessOperations.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_UsuarioConAutoriaClinica_DevuelveConflictoSinEliminar()
    {
        using var h = new Harness();
        var user = await h.CreateUserAsync("clinico@test.com");
        h.Users.ClinicalAuthorship = true;

        var (success, error, notFound) = await h.Users.DeleteAsync(user.Id);

        Assert.False(success);
        Assert.False(notFound);
        Assert.Contains("autoría clínica", error!);
        Assert.Empty(h.Users.Unlinked);
        Assert.Single(await h.Db.Users.ToListAsync());
    }
}
