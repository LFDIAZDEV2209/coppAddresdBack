using System.Security.Claims;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Resolución del actor de auditoría (HttpAuditActorContext) desde el JWT y el
/// HttpContext: claims del usuario autenticado y fallback a SYSTEM sin petición.
/// </summary>
public class HttpAuditActorContextTests
{
    private static HttpAuditActorContext Create(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "corr-123";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        return new HttpAuditActorContext(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void ConUsuarioAutenticado_ResuelveClaims()
    {
        var context = Create(
            new Claim(ClaimTypes.NameIdentifier, TestData.UserId.ToString()),
            new Claim(ClaimTypes.Email, "admin@coppaddresd.com"),
            new Claim(ClaimTypes.Role, "Admin"));

        Assert.Equal("USER", context.ActorType);
        Assert.Equal(TestData.UserId, context.UserId);
        Assert.Equal("admin@coppaddresd.com", context.UserEmail);
        Assert.Equal("Admin", context.UserRole);
        Assert.Equal("127.0.0.1", context.IpAddress);
        Assert.Equal("corr-123", context.CorrelationId);
        Assert.Equal("corr-123", context.RequestId);
    }

    [Fact]
    public void NameIdentifierInvalido_UserIdNull()
    {
        var context = Create(new Claim(ClaimTypes.NameIdentifier, "no-es-guid"));

        Assert.Equal("USER", context.ActorType);
        Assert.Null(context.UserId);
    }

    [Fact]
    public void SinHttpContext_ActorSystem()
    {
        var context = new HttpAuditActorContext(new HttpContextAccessor { HttpContext = null });

        Assert.Equal("SYSTEM", context.ActorType);
        Assert.Null(context.UserId);
        Assert.Null(context.UserEmail);
        Assert.Null(context.CorrelationId);
    }

    [Fact]
    public void SinUsuarioAutenticado_ActorSystem()
    {
        // Identity SIN tipo de autenticación → IsAuthenticated = false.
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "corr-123";
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        var actor = new HttpAuditActorContext(new HttpContextAccessor { HttpContext = context });

        Assert.Equal("SYSTEM", actor.ActorType);
        Assert.Null(actor.UserId);
    }
}