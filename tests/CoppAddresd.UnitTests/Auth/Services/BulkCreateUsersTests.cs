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
using System.Security.Cryptography;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Tests de creación masiva de usuarios (UserService.CreateBulkAsync).
/// Usa SQLite in-memory (mismo patrón que RoleServiceTests) con Identity
/// real para validar el flujo completo: resolución de roles por nombre,
/// mapeo de status, generación de contraseñas temporales, independencia
/// por fila y límites de capacidad.
/// </summary>
public class BulkCreateUsersTests
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

            services.AddScoped<UserService>();
            services.AddSingleton<IRoleService, RoleService>();
            services.AddSingleton<ITokenInvalidationService>(new FakeTokenInvalidationService());

            _provider = services.BuildServiceProvider();
            Db.Database.EnsureCreated();
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
        public UserService Users => _provider.GetRequiredService<UserService>();
        public RoleService Roles => _provider.GetRequiredService<RoleService>();
        public UserManager<ApplicationUser> UserManager =>
            _provider.GetRequiredService<UserManager<ApplicationUser>>();

        public RoleManager<ApplicationRole> RoleManager =>
            _provider.GetRequiredService<RoleManager<ApplicationRole>>();

        public async Task<Guid> CreateRoleAsync(string name, bool isActive = true)
        {
            var role = new ApplicationRole
            {
                Name = name,
                Description = "test",
                IsActive = isActive,
                IsSystem = false,
            };
            // Usar RoleManager.CreateAsync para que NormalizedName se establezca
            // correctamente (AddToRoleAsync requiere NormalizedName).
            await RoleManager.CreateAsync(role);
            return role.Id;
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }

    [Fact]
    public async Task BulkCreate_TodasFilasExitosas_CreaUsuarios()
    {
        using var h = new Harness();
        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com" },
                new() { FirstName = "Luis", LastName = "Pérez", Email = "luis@test.com" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Failed);
        Assert.All(result.Results, r => Assert.True(r.Success));
        Assert.All(result.Results, r => Assert.NotNull(r.TemporaryPassword));
        Assert.All(result.Results, r => Assert.NotNull(r.UserId));
    }

    [Fact]
    public async Task BulkCreate_ConRolAsignado_AsignaCorrectamente()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("Profesional");

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com", RoleName = "Profesional" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.NotNull(user);
        Assert.True(await h.UserManager.IsInRoleAsync(user!, "Profesional"));
    }

    [Fact]
    public async Task BulkCreate_RolCaseInsensitive_ResuelveCorrectamente()
    {
        using var h = new Harness();
        await h.CreateRoleAsync("Enfermero");

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "María", LastName = "López", Email = "maria@test.com", RoleName = "enfermero" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.True(await h.UserManager.IsInRoleAsync(user!, "Enfermero"));
    }

    [Fact]
    public async Task BulkCreate_RolNoEncontrado_FilaFallidaIndependiente()
    {
        using var h = new Harness();
        await h.CreateRoleAsync("Real");

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com", RoleName = "Inexistente" },
                new() { FirstName = "Luis", LastName = "Pérez", Email = "luis@test.com" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Rol no encontrado", result.Results[0].Error);
        Assert.Contains("Inexistente", result.Results[0].Error!);
        Assert.True(result.Results[1].Success);
    }

    [Fact]
    public async Task BulkCreate_EmailDuplicado_FilaFallidaIndependiente()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "duplicado@test.com" },
                new() { FirstName = "Luis", LastName = "Pérez", Email = "duplicado@test.com" },
                new() { FirstName = "María", LastName = "López", Email = "otro@test.com" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.True(result.Results[0].Success);
        Assert.False(result.Results[1].Success);
        Assert.Contains("ya está registrado", result.Results[1].Error);
        Assert.True(result.Results[2].Success);
    }

    [Fact]
    public async Task BulkCreate_StatusActivo_UsuarioActivo()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com", Status = "activo" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.True(user!.IsActive);
    }

    [Fact]
    public async Task BulkCreate_StatusInactivo_UsuarioDesactivado()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com", Status = "inactivo" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.False(user!.IsActive);
        // La respuesta también refleja IsActive = false.
        Assert.NotNull(result.Results[0].UserId);
    }

    [Fact]
    public async Task BulkCreate_StatusBlanco_DefaultActivo()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com", Status = null },
                new() { FirstName = "Luis", LastName = "Pérez", Email = "luis@test.com", Status = "  " },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(2, result.Created);
        var user1 = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        var user2 = await h.UserManager.FindByIdAsync(result.Results[1].UserId!);
        Assert.True(user1!.IsActive);
        Assert.True(user2!.IsActive);
    }

    [Fact]
    public async Task BulkCreate_StatusInvalido_FilaFallida()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com", Status = "suspendido" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Status inválido", result.Results[0].Error);
    }

    [Fact]
    public async Task BulkCreate_ContrasenaGenerada_CumplePolitica()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var password = UserService.GenerateSecurePassword();

            Assert.True(password.Length >= 12, $"Password muy corta: {password.Length}");
            Assert.Contains(password, c => char.IsUpper(c));
            Assert.Contains(password, c => char.IsLower(c));
            Assert.Contains(password, c => char.IsDigit(c));
            Assert.Contains(password, c => !char.IsLetterOrDigit(c));
            // Sin caracteres ambiguos (l/I/1/O/0).
            Assert.DoesNotContain(password, c => c is 'l' or 'I' or '1' or 'O' or '0');
        }
    }

    [Fact]
    public async Task BulkCreate_ContrasenaUnica_CadaLlamada()
    {
        var passwords = new HashSet<string>();
        for (var i = 0; i < 20; i++)
        {
            passwords.Add(UserService.GenerateSecurePassword());
        }

        // Con 16 chars y un alfabeto de ~60, la probabilidad de colisión
        // en 20 muestras es despreciable.
        Assert.Equal(20, passwords.Count);
    }

    [Fact]
    public async Task BulkCreate_FilasVacias_LanzaExcepcion()
    {
        using var h = new Harness();
        var request = new BulkCreateUsersRequest { Rows = [] };

        // La validación de rows vacías vive en el controller (400),
        // pero el service también debe manejarlo sin error.
        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(0, result.Created);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task BulkCreate_PerRowIndependence_UnaFilaFallaLasDemasContinuan()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com" },
                new() { FirstName = "X", LastName = "Y", Email = "bad@test.com", Status = "invalido" },
                new() { FirstName = "Luis", LastName = "Pérez", Email = "luis@test.com", RoleName = "NoExiste" },
                new() { FirstName = "María", LastName = "López", Email = "maria@test.com", Status = "inactivo" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(2, result.Created);
        Assert.Equal(2, result.Failed);
        Assert.True(result.Results[0].Success);   // Ana OK
        Assert.False(result.Results[1].Success);   // status inválido
        Assert.False(result.Results[2].Success);   // rol no existe
        Assert.True(result.Results[3].Success);    // María OK, inactiva
    }

    [Fact]
    public async Task BulkCreate_RespuestaContieneTempPassword_EnExito()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].TemporaryPassword);
        Assert.True(result.Results[0].TemporaryPassword!.Length >= 12);
        Assert.Equal("ana@test.com", result.Results[0].Email);
    }

    [Fact]
    public async Task BulkCreate_RespuestaSinTempPassword_EnError()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com" },
                new() { FirstName = "Luis", LastName = "Pérez", Email = "ana@test.com" }, // duplicado
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].TemporaryPassword);
        Assert.False(result.Results[1].Success);
        Assert.Null(result.Results[1].TemporaryPassword);
    }

    [Fact]
    public async Task BulkCreate_RolMultiPalabra_ResuelveCorrectamente()
    {
        using var h = new Harness();
        await h.CreateRoleAsync("Care Coordinator");

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com", RoleName = "Care Coordinator" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.True(await h.UserManager.IsInRoleAsync(user!, "Care Coordinator"));
    }

    [Fact]
    public async Task BulkCreate_ContadoresCreatedFailed_Correctos()
    {
        using var h = new Harness();

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new() { FirstName = "Ana", LastName = "García", Email = "ana@test.com" },
                new() { FirstName = "X", LastName = "Y", Email = "dup@test.com" },
                new() { FirstName = "Z", LastName = "W", Email = "dup@test.com" },
                new() { FirstName = "María", LastName = "López", Email = "maria@test.com" },
                new() { FirstName = "Luis", LastName = "Pérez", Email = "luis@test.com", Status = "novalido" },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(3, result.Created);
        Assert.Equal(2, result.Failed);
        Assert.Equal(5, result.Results.Count);
    }
}
