using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    public static AuthDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AuthDbContext(options);
    }

    /// <summary>
    /// UserManager sustituto (NSubstitute): los tests configuran explícitamente
    /// con .Returns(...) los métodos que ejercitan. Si se recibe un usuario
    /// semilla, FindByIdAsync lo devuelve por defecto (los servicios lo resuelven
    /// por Id antes de operar).
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
        }

        return userManager;
    }

    public static SignInManager<ApplicationUser> CreateSignInManager(
        UserManager<ApplicationUser> userManager
    )
    {
        var contextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var options = Options.Create(new IdentityOptions());
        var logger = Substitute.For<ILogger<SignInManager<ApplicationUser>>>();

        return new SignInManager<ApplicationUser>(
            userManager,
            contextAccessor,
            claimsFactory,
            options,
            logger,
            null!,
            null!
        );
    }
}
