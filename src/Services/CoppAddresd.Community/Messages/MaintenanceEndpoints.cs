using CoppAddresd.Community.Metrics;

namespace CoppAddresd.Community.Messages;

/// <summary>
/// Endpoints internos de mantenimiento del servicio de Comunidad. Se autentican
/// con el header <c>X-Internal-Key</c> (la misma clave que
/// <see cref="InternalMessageEndpoints"/>: configuración
/// <c>Community:InternalApiKey</c> o variable de entorno
/// <c>COMMUNITY_INTERNAL_API_KEY</c>), por lo que NO deben exponerse fuera de la
/// red interna. Si la clave no está configurada quedan deshabilitados (503) en
/// lugar de quedar abiertos sin autenticación.
/// </summary>
public static class MaintenanceEndpoints
{
    private const string InternalKeyHeader = "X-Internal-Key";

    public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/community/maintenance");

        // Reconciliación del rollup de métricas (community.community_daily_metrics):
        // reconstrucción autoritativa desde el OLTP. ?dryRun=true simula sin escribir.
        group
            .MapPost("/reconcile-metrics", ReconcileMetricsAsync)
            .WithName("CommunityReconcileMetrics")
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> ReconcileMetricsAsync(
        HttpRequest http,
        IConfiguration configuration,
        ICommunityMetricsBackfillService backfill,
        bool? dryRun,
        CancellationToken ct)
    {
        var expectedKey =
            configuration["Community:InternalApiKey"]
            ?? Environment.GetEnvironmentVariable("COMMUNITY_INTERNAL_API_KEY");

        // Si la clave no está configurada, el endpoint queda deshabilitado (503)
        // en lugar de quedar abierto sin autenticación.
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return Results.Problem(
                title: "Endpoint interno deshabilitado",
                detail: "Falta configurar Community:InternalApiKey.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (
            !http.Headers.TryGetValue(InternalKeyHeader, out var provided)
            || !string.Equals(provided.ToString(), expectedKey, StringComparison.Ordinal)
        )
        {
            return Results.Unauthorized();
        }

        var result = await backfill.BackfillAsync(dryRun ?? false, ct);
        return Results.Ok(result);
    }
}
