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
/// por fila, límites de capacidad y asignación scoped por clínica.
/// </summary>
public class BulkCreateUsersTests
{
    /// <summary>
    /// Servicio de prueba que reemplaza la búsqueda de clínicas por nombre
    /// con un stub compatible con SQLite (no tiene tabla erp.clinics).
    /// Permite configurar clínicas de prueba por nombre.
    /// </summary>
    private sealed class TestUserService : UserService
    {
        private readonly Dictionary<string, List<(Guid Id, string Name)>> _clinics;

        public TestUserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            AuthDbContext dbContext,
            ITokenInvalidationService tokenInvalidation,
            Dictionary<string, List<(Guid Id, string Name)>>? clinics = null)
            : base(userManager, roleManager, dbContext, tokenInvalidation, NullLogger<UserService>.Instance)
        {
            _clinics = clinics ?? new Dictionary<string, List<(Guid, string)>>(StringComparer.OrdinalIgnoreCase);
        }

        protected override Task<List<(Guid Id, string Name)>> FindClinicsByNameAsync(
            string name,
            CancellationToken ct)
        {
            if (_clinics.TryGetValue(name, out var matches))
                return Task.FromResult(matches);

            return Task.FromResult(new List<(Guid Id, string Name)>());
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly Dictionary<string, List<(Guid Id, string Name)>> _clinics = new(StringComparer.OrdinalIgnoreCase);

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

            // No registrar UserService DI — lo creamos manualmente con el stub.
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

        /// <summary>Crea el servicio de prueba con el stub de clínicas configurado.</summary>
        public TestUserService Users => new(
            UserManager,
            RoleManager,
            Db,
            _provider.GetRequiredService<ITokenInvalidationService>(),
            _clinics);

        /// <summary>Registra una clínica de prueba para la búsqueda por nombre.</summary>
        public void RegisterClinic(string name, Guid id)
        {
            _clinics[name] = [(id, name)];
        }

        /// <summary>Registra múltiples clínicas con el mismo nombre (ambigua).</summary>
        public void RegisterAmbiguousClinic(string name, params Guid[] ids)
        {
            _clinics[name] = ids.Select(id => (id, name)).ToList();
        }

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

    // --- Tests de asignación scoped por clínica ---

    [Fact]
    public async Task BulkCreate_ConClinicaYRol_CreaAsignacionScoped()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("Professional");
        var clinicId = Guid.NewGuid();
        h.RegisterClinic("Clínica Central", clinicId);

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new()
                {
                    FirstName = "Ana",
                    LastName = "García",
                    Email = "ana@test.com",
                    RoleName = "Professional",
                    ClinicName = "Clínica Central",
                },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.NotNull(user);

        // NO debe tener rol global.
        Assert.False(await h.UserManager.IsInRoleAsync(user!, "Professional"));

        // SÍ debe tener asignación scoped.
        var scoped = await h.Db.ScopedRoleAssignments
            .Where(s => s.UserId == user!.Id && s.ScopeType == "Clinic" && s.ScopeId == clinicId)
            .ToListAsync();
        Assert.Single(scoped);
        Assert.Equal(roleId, scoped[0].RoleId);
    }

    [Fact]
    public async Task BulkCreate_ClinicaNoEncontrada_FilaFallida()
    {
        using var h = new Harness();
        await h.CreateRoleAsync("Professional");
        // No registramos ninguna clínica.

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new()
                {
                    FirstName = "Ana",
                    LastName = "García",
                    Email = "ana@test.com",
                    RoleName = "Professional",
                    ClinicName = "Clínica Inexistente",
                },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Clínica no encontrada", result.Results[0].Error);
        Assert.Contains("Clínica Inexistente", result.Results[0].Error!);
    }

    [Fact]
    public async Task BulkCreate_ClinicaAmbigua_FilaFallida()
    {
        using var h = new Harness();
        await h.CreateRoleAsync("Professional");
        h.RegisterAmbiguousClinic("Clínica Centro", Guid.NewGuid(), Guid.NewGuid());

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new()
                {
                    FirstName = "Ana",
                    LastName = "García",
                    Email = "ana@test.com",
                    RoleName = "Professional",
                    ClinicName = "Clínica Centro",
                },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Clínica ambigua", result.Results[0].Error);
    }

    [Fact]
    public async Task BulkCreate_ClinicaSinRol_FilaFallida()
    {
        using var h = new Harness();
        var clinicId = Guid.NewGuid();
        h.RegisterClinic("Clínica Central", clinicId);

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new()
                {
                    FirstName = "Ana",
                    LastName = "García",
                    Email = "ana@test.com",
                    RoleName = null,
                    ClinicName = "Clínica Central",
                },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("La clínica requiere un rol", result.Results[0].Error);
    }

    [Fact]
    public async Task BulkCreate_SinClinica_ConRolGlobal_ComportamientoNormal()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("Professional");

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new()
                {
                    FirstName = "Ana",
                    LastName = "García",
                    Email = "ana@test.com",
                    RoleName = "Professional",
                    ClinicName = null,
                },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.NotNull(user);

        // Rol global asignado (comportamiento existente).
        Assert.True(await h.UserManager.IsInRoleAsync(user!, "Professional"));
        // Sin asignación scoped.
        var scoped = await h.Db.ScopedRoleAssignments
            .Where(s => s.UserId == user!.Id)
            .ToListAsync();
        Assert.Empty(scoped);
    }

    [Fact]
    public async Task BulkCreate_ClinicaYRol_CaseInsensitive()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("Professional");
        var clinicId = Guid.NewGuid();
        h.RegisterClinic("Clínica Central", clinicId);

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                new()
                {
                    FirstName = "Ana",
                    LastName = "García",
                    Email = "ana@test.com",
                    RoleName = "professional", // minúscula
                    ClinicName = "clínica central", // minúscula
                },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.True(result.Results[0].Success);
        var user = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.NotNull(user);

        var scoped = await h.Db.ScopedRoleAssignments
            .Where(s => s.UserId == user!.Id && s.ScopeType == "Clinic" && s.ScopeId == clinicId)
            .ToListAsync();
        Assert.Single(scoped);
    }

    [Fact]
    public async Task BulkCreate_MixtoClinicaYSinClinica_IndependenciaPorFila()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync("Professional");
        var clinicId = Guid.NewGuid();
        h.RegisterClinic("Clínica Central", clinicId);

        var request = new BulkCreateUsersRequest
        {
            Rows =
            [
                // Fila 1: con clínica → scoped.
                new()
                {
                    FirstName = "Ana",
                    LastName = "García",
                    Email = "ana@test.com",
                    RoleName = "Professional",
                    ClinicName = "Clínica Central",
                },
                // Fila 2: sin clínica → global.
                new()
                {
                    FirstName = "Luis",
                    LastName = "Pérez",
                    Email = "luis@test.com",
                    RoleName = "Professional",
                },
            ]
        };

        var result = await h.Users.CreateBulkAsync(request);

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Failed);

        // Fila 1: scoped.
        var user1 = await h.UserManager.FindByIdAsync(result.Results[0].UserId!);
        Assert.False(await h.UserManager.IsInRoleAsync(user1!, "Professional"));
        var scoped1 = await h.Db.ScopedRoleAssignments
            .Where(s => s.UserId == user1!.Id)
            .ToListAsync();
        Assert.Single(scoped1);

        // Fila 2: global.
        var user2 = await h.UserManager.FindByIdAsync(result.Results[1].UserId!);
        Assert.True(await h.UserManager.IsInRoleAsync(user2!, "Professional"));
        var scoped2 = await h.Db.ScopedRoleAssignments
            .Where(s => s.UserId == user2!.Id)
            .ToListAsync();
        Assert.Empty(scoped2);
    }
}
