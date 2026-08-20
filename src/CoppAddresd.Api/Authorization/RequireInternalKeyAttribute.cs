using CoppAddresd.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Authorization;

/// <summary>
/// Exige el header <c>X-Internal-Key</c> con el valor de
/// <c>Telemedicine:InternalApiKey</c>. Protege los endpoints internos de datos
/// de referencia que consume el microservicio de Telemedicina (mismo patrón
/// que <c>RequireInternalKeyAttribute</c> del Auth Service). No requiere JWT:
/// la clave interna es la credencial servicio-a-servicio.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireInternalKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<TelemedicineServiceSettings>>().Value;

        var provided = context.HttpContext.Request.Headers["X-Internal-Key"].ToString();

        if (string.IsNullOrEmpty(options.InternalApiKey)
            || !FixedTimeEquals(options.InternalApiKey, provided))
        {
            context.Result = new UnauthorizedObjectResult(
                new { message = "Clave interna inválida." });
            return;
        }

        await Task.CompletedTask;
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var a = System.Text.Encoding.UTF8.GetBytes(expected);
        var b = System.Text.Encoding.UTF8.GetBytes(actual);
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
