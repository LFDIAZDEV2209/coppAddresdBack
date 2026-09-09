using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Auth.TestDoubles;

/// <summary>
/// Helpers para crear instancias InMemory de AuthDbContext y managers
/// de Identity para tests unitarios.
/// </summary>
public static class IdentityTestDoubles
{
    /// <summary>
    /// Contexto InMemory para tests de LÓGICA (sync de roles/permisos, estados).
    /// Los servicios abren transacciones explícitas (UserService); el proveedor
    /// InMemory no las implementa (no-op): la advertencia TransactionIgnoredWarning
    /// se ignora a propósito para que esos flujos no estallen.
    /// Para comportamiento relacional real (ExecuteUpdateAsync, concurrencia)
    /// usar CreateSqliteDbContext.
    /// </summary>
    public static AuthDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AuthDbContext(options);
    }

    /// <summary>
    /// Crea un AuthDbContext sobre SQLite EN MEMORIA con el esquema completo del
    /// modelo. A diferencia del proveedor InMemory, SQLite ejecuta UPDATEs
    /// condicionales (ExecuteUpdateAsync) y transacciones reales — requisito de
    /// los tests de rotación de refresh, invalidación batch y claims.
    /// La conexión se devuelve ABIERTA a propósito: la BD en memoria vive
    /// mientras haya al menos una conexión abierta. El llamador es responsable
    /// de disponer TANTO el contexto como la conexión (patrón fixture IDisposable).
    /// </summary>
    /// <param name="dbName">
    /// Nombre de la BD compartida. Si se omite, cada llamada crea una BD única.
    /// Si se provee el MISMO nombre en varias llamadas, todas comparten la misma
    /// BD en memoria (Mode=Memory + Cache=Shared) — usado por los tests de
    /// concurrencia que necesitan dos contextos sobre la misma base.
    /// </param>
    public static (AuthDbContext Db, SqliteConnection Connection) CreateSqliteDbContext(
        string? dbName = null)
    {
        // Conexión en memoria: :memory: para BD única; nombre + Mode=Memory +
        // Cache=Shared para compartir la BD entre conexiones del mismo nombre.
        var connectionString = dbName is null
            ? "DataSource=:memory:"
            : $"Data Source={dbName};Mode=Memory;Cache=Shared";

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite(connection)
            .Options;

        return (new AuthDbContext(options), connection);
    }

    /// <summary>
    /// UserManager sustituto (NSubstitute): los tests configuran explícitamente
    /// con .Returns(...) los métodos que ejercitan. Si se recibe un usuario
    /// semilla, FindByIdAsync y FindByEmailAsync (por el email del usuario)
    /// lo devuelven por defecto (los servicios resuelven el usuario por Id o
    /// por email antes de operar); cualquier stub posterior del test gana.
    /// </summary>
    public static UserManager<ApplicationUser> CreateUserManager(ApplicationUser? seedUser = null)
    {
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>()
        );

        if (seedUser is not null)
        {
            userManager.FindByIdAsync(Arg.Any<string>()).Returns(seedUser);
            userManager.FindByEmailAsync(seedUser.Email!).Returns(seedUser);
        }

        return userManager;
    }

    /// <summary>
    /// SignInManager sustituto (NSubstitute): los tests de login configuran
    /// CheckPasswordSignInAsync con .Returns(SignInResult.Success). NO puede
    /// ser una instancia real: el flujo real consulta el UserManager sustituto
    /// (SupportsUserLockout, etc.) y NSubstitute enlazaría el Returns a esa
    /// llamada interna (type mismatch). El substitute recibe la misma firma
    /// del constructor real; los métodos no configurados devuelven el default
    /// (CheckPasswordSignInAsync sin stub → SignInResult null → login falla).
    /// </summary>
    public static SignInManager<ApplicationUser> CreateSignInManager(
        UserManager<ApplicationUser> userManager
    )
    {
        var contextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var options = Options.Create(new IdentityOptions());
        var logger = Substitute.For<ILogger<SignInManager<ApplicationUser>>>();

        return Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            contextAccessor,
            claimsFactory,
            options,
            logger,
            null!,
            null!
        );
    }

    /// <summary>
    /// RoleManager sustituto (NSubstitute): los tests de UserService no ejercitan
    /// el manager de roles (el sync resuelve los RoleIds contra el AuthDbContext,
    /// no contra el RoleManager), por lo que se entrega un substitute sin
    /// configuración. Si un test lo necesita, configura .Returns(...) los
    /// métodos que ejercita, igual que con CreateUserManager.
    /// </summary>
    public static RoleManager<ApplicationRole> CreateRoleManager()
    {
        return Substitute.For<RoleManager<ApplicationRole>>(
            Substitute.For<IRoleStore<ApplicationRole>>(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<ILogger<RoleManager<ApplicationRole>>>()
        );
    }
}
