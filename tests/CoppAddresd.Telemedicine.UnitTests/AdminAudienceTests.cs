using System.Security.Claims;
using CoppAddresd.Telemedicine.Authorization;
using CoppAddresd.Telemedicine.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

public sealed class AdminAudienceTests
{
    [Theory]
    [InlineData("Appointments.AdminView", "app", true, false)]
    [InlineData("Telemedicine.AdminView", "app", true, false)]
    [InlineData("Appointments.AdminView", "app", false, false)]
    [InlineData("Appointments.AdminView", null, true, false)]
    [InlineData("Appointments.AdminView", "erp", true, true)]
    [InlineData("Telemedicine.AdminView", "erp", true, true)]
    [InlineData("Appointments.AdminView", "erp", false, true)]
    [InlineData("Appointments.View", "app", true, true)]
    public async Task Handle_AdminExigeErpSinBloquearPermisosCompartidos(
        string permission,
        string? audience,
        bool global,
        bool expected
    )
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (global)
            claims.Add(new("permission", permission));
        if (audience is not null)
            claims.Add(new("aud", audience));
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            null
        );
        var scoped = new ScopedAccess();
        var handler = new PermissionHandler(
            scoped,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            NullLogger<PermissionHandler>.Instance
        );
        await handler.HandleAsync(context);
        Assert.Equal(expected, context.HasSucceeded);
        if (!expected)
            Assert.Equal(0, scoped.Calls);
    }

    private sealed class ScopedAccess : ITelemedicineScopedAuthorizationClient
    {
        public int Calls { get; private set; }

        public Task<bool> AuthorizeAsync(
            Guid userId,
            string stamp,
            string permission,
            IReadOnlyList<ScopeEntry> chain,
            CancellationToken ct = default
        )
        {
            Calls++;
            return Task.FromResult(true);
        }
    }
}
