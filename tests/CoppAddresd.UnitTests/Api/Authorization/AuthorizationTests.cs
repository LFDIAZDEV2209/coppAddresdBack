using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests.Api.Authorization;

public class PermissionHandlerTests
{
    private static ClaimsPrincipal UserWithPermissions(params string[] permissions)
    {
        var claims = permissions
            .Select(p => new Claim(PermissionClaimTypes.Permission, p))
            .ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string permissionCode)
    {
        var handler = new PermissionHandler(NullLogger<PermissionHandler>.Instance);
        var requirement = new PermissionRequirement(permissionCode);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Handle_UsuarioConPermiso_Succeeded()
    {
        var user = UserWithPermissions("Patients.View", "Employees.View");
        Assert.True(await AuthorizeAsync(user, "Patients.View"));
    }

    [Fact]
    public async Task Handle_UsuarioSinPermiso_Falla()
    {
        var user = UserWithPermissions("Patients.View");
        Assert.False(await AuthorizeAsync(user, "Employees.View"));
    }

    [Fact]
    public async Task Handle_UsuarioSinClaims_Falla()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.False(await AuthorizeAsync(user, "Patients.View"));
    }
}

public class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider CreateProvider()
        => new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task GetPolicy_CodigoConocido_CreaRequerimientoDePermiso()
    {
        var provider = CreateProvider();
        var policy = await provider.GetPolicyAsync(PermissionCodes.EmployeesView);

        Assert.NotNull(policy);
        Assert.Single(policy!.Requirements);
        Assert.IsType<PermissionRequirement>(policy.Requirements[0]);
    }

    [Fact]
    public async Task GetPolicy_CodigoDesconocido_DelegaAlFallback()
    {
        var provider = CreateProvider();
        var policy = await provider.GetPolicyAsync("NoExiste");
        Assert.Null(policy);
    }
}

public class ScopeEntryTests
{
    [Fact]
    public void EncodeChain_FormatoEsperado()
    {
        var chain = new List<ScopeEntry>
        {
            new("Clinic", Guid.Parse("360a13fe-8adc-4a00-94df-04d2fa703b7c")),
            new("Organization", Guid.Parse("5fde219a-89ea-4cf9-be48-379e8b1042cb")),
            ScopeEntry.Global,
        };

        var encoded = ScopeEntry.EncodeChain(chain);

        Assert.Equal(
            "Clinic:360a13fe-8adc-4a00-94df-04d2fa703b7c|Organization:5fde219a-89ea-4cf9-be48-379e8b1042cb|Global",
            encoded);
    }

    [Fact]
    public void Global_ScopeIdEsNull()
        => Assert.Null(ScopeEntry.Global.ScopeId);
}
