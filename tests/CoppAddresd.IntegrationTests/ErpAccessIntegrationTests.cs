using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

/// <summary>Cada caso crea y elimina su propia BD PostgreSQL; nunca modifica tablas de desarrollo.</summary>
public sealed class ErpAccessIntegrationTests : IAsyncLifetime
{
    private readonly string _database =
        "coppaddresd_erp_access_test_" + Guid.NewGuid().ToString("N");
    private string _adminConnection = null!;
    private ServiceProvider _provider = null!;
    private bool _created;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();
    private Guid _erpId;
    private Guid _appId;
    private const string Password = "Integration!Pass2026";

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException(
                "COP_TEST_DB_CONNECTION es obligatorio para estas pruebas reales."
            );
        var builder = new NpgsqlConnectionStringBuilder(source)
        {
            Database = "postgres",
            Pooling = false,
        };
        _adminConnection = builder.ConnectionString;
        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE \"{_database}\" TEMPLATE template0",
                admin
            );
            await create.ExecuteNonQueryAsync();
            _created = true;
        }
        builder.Database = _database;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddDbContext<AuthDbContext>(o => o.UseNpgsql(builder.ConnectionString));
        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager();
        services.Configure<JwtSettings>(o =>
        {
            o.Secret = new string('x', 64);
            o.Issuer = "integration-tests";
            o.AccessTokenExpirationMinutes = 15;
            o.RefreshTokenExpirationDays = 7;
        });
        var permissions = Substitute.For<IPermissionService>();
        permissions
            .GetUserAllPermissionCodesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());
        services.AddSingleton(permissions);
        services.AddSingleton(Substitute.For<IPatientLookupService>());
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<ErpAccessService>();
        services.AddScoped<CoppAddresd.Auth.Security.SecurityStampValidator>();
        _provider = services.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.EnsureCreatedAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = _userId,
            UserName = "erp-integration@example.test",
            Email = "erp-integration@example.test",
            FirstName = "Ana",
            LastName = "Test",
            IsActive = true,
        };
        var result = await manager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Description)));
        var erp = new CoppAddresd.Auth.Entities.Application
        {
            Id = Guid.NewGuid(),
            Code = "erp",
            Name = "ERP",
            IsActive = true,
        };
        var app = new CoppAddresd.Auth.Entities.Application
        {
            Id = Guid.NewGuid(),
            Code = "app",
            Name = "App",
            IsActive = true,
        };
        _erpId = erp.Id;
        _appId = app.Id;
        db.Applications.AddRange(erp, app);
        db.UserApplications.AddRange(
            new UserApplication { UserId = _userId, ApplicationId = erp.Id },
            new UserApplication { UserId = _userId, ApplicationId = app.Id }
        );
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
        if (!_created)
            return;
        if (!_database.StartsWith("coppaddresd_erp_access_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Nombre de BD de pruebas inválido.");
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE \"{_database}\" WITH (FORCE)",
            admin
        );
        await drop.ExecuteNonQueryAsync();
    }

    private async Task<TokenResult?> Login(string application)
    {
        using var scope = _provider.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<AuthService>()
            .LoginAsync(
                new LoginRequest
                {
                    Email = "erp-integration@example.test",
                    Password = Password,
                    Application = application,
                }
            );
    }

    private async Task<TokenResult?> Refresh(string token)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuthService>().RefreshAsync(token);
    }

    private async Task<ErpAccessOperation> Change(Guid id, string status)
    {
        using var scope = _provider.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<ErpAccessService>()
            .ChangeAsync(id, _userId, _employeeId, status, CancellationToken.None);
    }

    private async Task<bool> ValidateErp(string token)
    {
        using var scope = _provider.CreateScope();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var claims = jwt.Claims.ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, _userId.ToString()));
        return await scope
            .ServiceProvider.GetRequiredService<CoppAddresd.Auth.Security.SecurityStampValidator>()
            .ValidateAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
    }

    [Fact]
    public async Task SuspensionYReactivacion_PreservaAppYNoReviveJwtErpAnteriores()
    {
        var erp = Assert.IsType<TokenResult>(await Login("erp"));
        var app = Assert.IsType<TokenResult>(await Login("app"));
        Assert.True(await ValidateErp(erp.AccessToken));
        var operation = await Change(Guid.NewGuid(), "Inactive");
        Assert.False(await ValidateErp(erp.AccessToken));
        Assert.Null(await Login("erp"));
        Assert.Null(await Refresh(erp.RefreshToken));
        Assert.NotNull(await Login("app"));
        var refreshedApp = Assert.IsType<TokenResult>(await Refresh(app.RefreshToken));
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.True((await db.Users.SingleAsync(x => x.Id == _userId)).IsActive);
            var appAccess = await db.UserApplications.SingleAsync(x =>
                x.UserId == _userId && x.ApplicationId == _appId
            );
            Assert.False(appAccess.IsSuspended);
            Assert.Equal(0, appAccess.SessionVersion);
            Assert.NotNull(
                (await db.RefreshTokens.SingleAsync(x => x.Token == erp.RefreshToken)).RevokedAt
            );
            await scope
                .ServiceProvider.GetRequiredService<ErpAccessService>()
                .CompleteAsync(operation.Id, CancellationToken.None);
        }
        await Change(Guid.NewGuid(), "Active");
        Assert.False(await ValidateErp(erp.AccessToken));
        Assert.Null(await Refresh(erp.RefreshToken));
        Assert.NotNull(await Refresh(refreshedApp.RefreshToken));
        var newErp = Assert.IsType<TokenResult>(await Login("erp"));
        Assert.True(await ValidateErp(newErp.AccessToken));
    }

    [Fact]
    public async Task ReintentoMismaOperacion_NoDuplicaJournalNiIncrementaVersion()
    {
        var id = Guid.NewGuid();
        var first = await Change(id, "Inactive");
        var retry = await Change(id, "Inactive");
        Assert.Equal(first.SessionVersion, retry.SessionVersion);
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Equal(1, await db.ErpAccessOperations.CountAsync());
        Assert.Equal(
            1,
            (await db.UserApplications.SingleAsync(x => x.ApplicationId == _erpId)).SessionVersion
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() => Change(id, "Active"));
    }

    [Fact]
    public async Task CambiosConcurrentes_UnaOperacionPendienteYUnaVersionPersistida()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> Attempt()
        {
            await gate.Task;
            try
            {
                await Change(Guid.NewGuid(), "Inactive");
                return null;
            }
            catch (Exception e) when (e is InvalidOperationException or DbUpdateException)
            {
                return e;
            }
        }
        var first = Attempt();
        var second = Attempt();
        gate.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, e => e is null);
        Assert.Single(results, e => e is not null);
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Equal(1, await db.ErpAccessOperations.CountAsync(x => x.CompletedAt == null));
        Assert.Equal(
            1,
            (await db.UserApplications.SingleAsync(x => x.ApplicationId == _erpId)).SessionVersion
        );
    }

    [Fact]
    public async Task JournalRechazado_RevierteSuspensionVersionYRevocacionRefresh()
    {
        var token = Assert.IsType<TokenResult>(await Login("erp"));
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            // Fallo real de PostgreSQL después del UPDATE de refresh dentro de la transacción.
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE auth.\"ErpAccessOperations\" ADD CONSTRAINT reject_test_operation CHECK (\"Status\" <> 'Inactive')"
            );
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => Change(Guid.NewGuid(), "Inactive"));
        using var verification = _provider.CreateScope();
        var check = verification.ServiceProvider.GetRequiredService<AuthDbContext>();
        var access = await check.UserApplications.SingleAsync(x => x.ApplicationId == _erpId);
        Assert.False(access.IsSuspended);
        Assert.Equal(0, access.SessionVersion);
        Assert.Empty(await check.ErpAccessOperations.ToListAsync());
        Assert.Null(
            (await check.RefreshTokens.SingleAsync(x => x.Token == token.RefreshToken)).RevokedAt
        );
        Assert.NotNull(await Refresh(token.RefreshToken));
    }
}
