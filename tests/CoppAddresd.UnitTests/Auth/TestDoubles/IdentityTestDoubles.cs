using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Auth.TestDoubles;

/// <summary>
/// Helpers de identidad para tests de Auth.
/// Provee un AuthDbContext en memoria aislado por llamada y dobles
/// de UserManager/SignInManager requeridos por InvalidationVerificationTests.
/// </summary>
public static class IdentityTestDoubles
{
    /// <summary>
    /// Crea un AuthDbContext en memoria aislado por llamada.
    /// Usa InMemory con nombre único por invocación para garantizar aislamiento
    /// entre tests (evita colisiones y FK de SQLite que romperían el seed con
    /// UserRoles huérfanos).
    /// </summary>
    public static AuthDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AuthDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Crea un UserManager mock que resuelve FindByIdAsync al usuario dado.
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
        var opts = Options.Create(new IdentityOptions>();
        var logger = Substitute.For<ILogger<SignInManager<ApplicationUser>>>();
        var schemes = Substitute.For<IAuthenticationSchemeProvider>();
        var confirmation = Substitute.For<IUserConfirmation<ApplicationUser>>();

        var manager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager, contextAccessor, claimsFactory, opts, logger, schemes, confirmation);

        return manager;
    }
}
