using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using CoppAddresd.UnitTests.Auth.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using AuthApplication = CoppAddresd.Auth.Entities.Application;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Enforcement del bloqueo por estado del paciente en <see cref="AuthService"/>:
/// con el guard reportando un perfil "Inactivo", el login se corta antes de
/// verificar la contraseña y el refresh antes de rotar el token. Un usuario sin
/// perfil inactivo (staff) inicia sesión con normalidad.
/// </summary>
public sealed class AuthServicePatientAccessTests
{
    private const string RefreshTokenValue = "refresh-token-1";

    private sealed class Seed
    {
        public AuthDbContext Db { get; }
        public ApplicationUser User { get; }
        public AuthApplication App { get; }
        public RefreshToken? StoredToken { get; }

        public Seed(bool withRefreshToken = false)
        {
            Db = IdentityTestDoubles.CreateInMemoryDbContext();

            User = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "patient@demo.com",
                Email = "patient@demo.com",
                FirstName = "Patient",
                LastName = "Demo",
                IsActive = true
            };

            App = new AuthApplication
            {
                Id = Guid.NewGuid(),
                Code = "app",
                Name = "App móvil",
                IsActive = true
            };

            Db.Users.Add(User);
            Db.Applications.Add(App);
            Db.UserApplications.Add(new UserApplication
            {
                UserId = User.Id,
                User = User,
                ApplicationId = App.Id,
                Application = App,
                IsSuspended = false,
                SessionVersion = 0
            });

            if (withRefreshToken)
            {
                StoredToken = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = User.Id,
                    ApplicationId = App.Id,
                    Token = RefreshTokenValue,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                    CreatedAt = DateTime.UtcNow,
                    User = User,
                    Application = App
                };
                Db.RefreshTokens.Add(StoredToken);
            }

            Db.SaveChanges();
        }
    }

    private static AuthService CreateAuthService(
        Seed seed,
        IPatientAccessGuard guard,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IPermissionService permissionService
    ) =>
        new(
            userManager,
            signInManager,
            tokenService,
            permissionService,
            Substitute.For<IPatientLookupService>(),
            guard,
            seed.Db,
            Options.Create(new JwtSettings { AccessTokenExpirationMinutes = 15 }),
            NullLogger<AuthService>.Instance
        );

    [Fact]
    public async Task LoginAsync_PacienteInactivo_DevuelveNullYNoEmiteTokens()
    {
        var seed = new Seed();
        var guard = Substitute.For<IPatientAccessGuard>();
        guard.IsBlockedAsync(seed.User.Id, Arg.Any<CancellationToken>()).Returns(true);
        var userManager = IdentityTestDoubles.CreateUserManager(seed.User);
        userManager.FindByEmailAsync(seed.User.Email!).Returns(seed.User);
        var signInManager = IdentityTestDoubles.CreateSignInManager(userManager);
        var tokenService = Substitute.For<ITokenService>();
        var authService = CreateAuthService(
            seed,
            guard,
            userManager,
            signInManager,
            tokenService,
            Substitute.For<IPermissionService>()
        );

        var result = await authService.LoginAsync(
            new LoginRequest
            {
                Email = seed.User.Email,
                Password = "Whatever1!",
                Application = "app"
            }
        );

        Assert.Null(result);
        // El guard se consulta y el corte ocurre ANTES de verificar la contraseña.
        await guard.Received(1).IsBlockedAsync(seed.User.Id, Arg.Any<CancellationToken>());
        await signInManager
            .DidNotReceive()
            .CheckPasswordSignInAsync(seed.User, Arg.Any<string>(), Arg.Any<bool>());
        tokenService
            .DidNotReceiveWithAnyArgs()
            .GenerateAccessToken(default!, default!, default!, default!);
    }

    [Fact]
    public async Task LoginAsync_SinPerfilInactivo_EmiteTokens()
    {
        var seed = new Seed();
        var guard = Substitute.For<IPatientAccessGuard>(); // IsBlockedAsync => false
        var userManager = IdentityTestDoubles.CreateUserManager(seed.User);
        userManager.FindByEmailAsync(seed.User.Email!).Returns(seed.User);
        userManager.GetRolesAsync(seed.User).Returns(new List<string> { "Patient" });
        var signInManager = IdentityTestDoubles.CreateSignInManager(userManager);
        signInManager.CheckPasswordSignInAsync(seed.User, "Pass1!", true).Returns(SignInResult.Success);
        var tokenService = Substitute.For<ITokenService>();
        tokenService
            .GenerateAccessToken(
                Arg.Any<ApplicationUser>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<long>()
            )
            .Returns("access-token");
        tokenService
            .GenerateRefreshTokenAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long>()
            )
            .Returns("new-refresh-token");
        var permissionService = Substitute.For<IPermissionService>();
        permissionService
            .GetUserAllPermissionCodesAsync(seed.User.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());
        var authService = CreateAuthService(
            seed,
            guard,
            userManager,
            signInManager,
            tokenService,
            permissionService
        );

        var result = await authService.LoginAsync(
            new LoginRequest
            {
                Email = seed.User.Email,
                Password = "Pass1!",
                Application = "app"
            }
        );

        Assert.NotNull(result);
        Assert.Equal("access-token", result!.AccessToken);
        Assert.Equal("new-refresh-token", result.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_PacienteInactivo_NoRotaElTokenNiEmiteTokens()
    {
        var seed = new Seed(withRefreshToken: true);
        var guard = Substitute.For<IPatientAccessGuard>();
        guard.IsBlockedAsync(seed.User.Id, Arg.Any<CancellationToken>()).Returns(true);
        var userManager = IdentityTestDoubles.CreateUserManager(seed.User);
        var tokenService = Substitute.For<ITokenService>();
        var authService = CreateAuthService(
            seed,
            guard,
            userManager,
            IdentityTestDoubles.CreateSignInManager(userManager),
            tokenService,
            Substitute.For<IPermissionService>()
        );

        var result = await authService.RefreshAsync(RefreshTokenValue);

        Assert.Null(result);
        // El token no se revoca (no se quema) y no se emite uno nuevo.
        Assert.Null(seed.StoredToken!.RevokedAt);
        Assert.Null(seed.StoredToken.ReplacedByTokenId);
        await tokenService
            .DidNotReceiveWithAnyArgs()
            .GenerateRefreshTokenAsync(default, default, default, default);
    }
}
