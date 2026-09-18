using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests.Api.Authorization;

/// <summary>
/// Tests de <see cref="RequireInternalKeyAttribute"/> (clave interna compartida
/// con el microservicio de Telemedicina, header <c>X-Internal-Key</c>): clave
/// válida → continúa; ausente/incorrecta o clave sin configurar → 401. Es el
/// guard del endpoint interno de notificaciones (F2).
/// </summary>
public sealed class InternalKeyAuthorizationTests
{
    private const string ConfiguredKey = "internal-test-key";

    private static async Task<AuthorizationFilterContext> InvokeAsync(
        string? headerValue, string? configuredKey = ConfiguredKey)
    {
        var services = new ServiceCollection();
        services.Configure<TelemedicineServiceSettings>(options =>
        {
            options.InternalApiKey = configuredKey ?? string.Empty;
        });
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        if (headerValue is not null)
        {
            httpContext.Request.Headers["X-Internal-Key"] = headerValue;
        }

        var context = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);

        await new RequireInternalKeyAttribute().OnAuthorizationAsync(context);
        return context;
    }

    [Fact]
    public async Task Clave_valida_continua_sin_resultado()
    {
        var context = await InvokeAsync(ConfiguredKey);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Sin_header_devuelve_401()
    {
        var context = await InvokeAsync(headerValue: null);

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task Clave_incorrecta_devuelve_401()
    {
        var context = await InvokeAsync("otra-clave");

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task Clave_no_configurada_devuelve_401_aunque_coincida_el_header()
    {
        var context = await InvokeAsync(headerValue: string.Empty, configuredKey: string.Empty);

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }
}
