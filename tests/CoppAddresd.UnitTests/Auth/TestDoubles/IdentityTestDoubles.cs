using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Auth.TestDoubles;

/// <summary>
/// Helpers de identidad para tests de Auth.
/// Provee contextos en memoria (InMemory/SQLite) y dobles de
/// UserManager/SignInManager/RoleManager.
/// </summary>
public static class IdentityTestDoubles
{
    /// <summary>
    /// Crea un AuthDbContext InMemory aislado por llamada (EnsureCreated).
    /// Los servicios que abren transacciones explícitas (UserService) obtienen
    /// un no-op del proveedor InMemory: se ignora la advertencia
    /// TransactionIgnoredWarning a propósito para que esos flujos no estallen.
    /// Para comportamiento relacional real (ExecuteUpdateAsync, transacciones)
    /// usar <see cref="CreateSqliteDbContext"/>.
    /// </summary>
    public static AuthDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AuthDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Crea un AuthDbContext sobre SQLite EN MEMORIA con el esquema completo del
    /// modelo: ejecuta UPDATEs condicionales (ExecuteUpdateAsync) y transacciones
    /// reales, a diferencia del proveedor InMemory. La conexión se devuelve
    /// ABIERTA (la BD vive mientras haya conexiones abiertas); el llamador debe
    /// disponer contexto y conexión. Un mismo dbName comparte la BD (tests de
    /// concurrencia con dos contextos).
    /// </summary>
    public static (AuthDbContext Db, SqliteConnection Connection) CreateSqliteDbContext(
        string? dbName = null)
    {
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
    /// Crea un UserManager mock que resuelve FindByIdAsync al usuario dado
    /// (solo cuando el id coincide; resto → null, para ejercitar rutas de
    /// "usuario no encontrado").
    /// </summary>
    public static UserManager<ApplicationUser> CreateUserManager(ApplicationUser user)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var opts = Options.Create(new IdentityOptions());
        var hasher = Substitute.For<IPasswordHasher<ApplicationUser>>();
        var userValidators = Array.Empty<IUserValidator<ApplicationUser>>();
        var passwordValidators = Array.Empty<IPasswordValidator<ApplicationUser>>();
        var normalizer = Substitute.For<ILookupNormalizer>();
        var errors = new IdentityErrorDescriber();
        var services = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<UserManager<ApplicationUser>>>();

        var manager = Substitute.For<UserManager<ApplicationUser>>(
            store, opts, hasher, userValidators, passwordValidators, normalizer, errors, services, logger);

        manager.FindByIdAsync(Arg.Any<string>()).Returns(callInfo =>
        {
            var id = callInfo.Arg<string>();
            return id == user.Id.ToString() ? user : null!;
        });
        manager.FindByIdAsync(user.Id.ToString()).Returns(user);

        return manager;
    }

    /// <summary>
    /// Crea un SignInManager mock mínimo para AuthService.
    /// </summary>
    public static SignInManager<ApplicationUser> CreateSignInManager(UserManager<ApplicationUser> userManager)
    {
        var contextAccessor = Substitute.For<IHttpContextAccessor>();
        var claimsFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var opts = Options.Create(new IdentityOptions());
        var logger = Substitute.For<ILogger<SignInManager<ApplicationUser>>>();
        var schemes = Substitute.For<IAuthenticationSchemeProvider>();
        var confirmation = Substitute.For<IUserConfirmation<ApplicationUser>>();

        var manager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager, contextAccessor, claimsFactory, opts, logger, schemes, confirmation);

        return manager;
    }

    /// <summary>
    /// RoleManager sustituto (NSubstitute): los tests no ejercitan el manager de
    /// roles (el sync resuelve RoleIds contra el AuthDbContext); se entrega un
    /// substitute sin configuración.
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
