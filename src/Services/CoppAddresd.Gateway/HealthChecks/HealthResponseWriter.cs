using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoppAddresd.Gateway.HealthChecks;

/// <summary>
/// Escribe la respuesta de <c>/health</c> con el shape
/// <c>{"status":"Healthy","clusters":{"auth":"Healthy","api":"Healthy","telemedicine":"Healthy"}}</c>.
/// Cada entrada del <see cref="HealthReport"/> corresponde a un cluster; el status global
/// es <c>Healthy</c> (200) si todos están sanos, o <c>Degraded</c> (503) si alguno no lo está.
/// </summary>
public static class HealthResponseWriter
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        var clusters = new Dictionary<string, string>(StringComparer.Ordinal);
        var anyUnhealthy = false;

        foreach (var (name, entry) in report.Entries)
        {
            clusters[name] = StatusToString(entry.Status);
            if (entry.Status != HealthStatus.Healthy)
            {
                anyUnhealthy = true;
            }
        }

        var status = anyUnhealthy ? "Degraded" : "Healthy";
        var statusCode = anyUnhealthy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            status,
            clusters
        }, new JsonSerializerOptions { WriteIndented = false });
    }

    private static string StatusToString(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "Healthy",
        HealthStatus.Degraded => "Degraded",
        HealthStatus.Unhealthy => "Unhealthy",
        _ => "Unknown"
    };
}
