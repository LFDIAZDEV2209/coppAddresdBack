using Serilog.Context;

namespace CoppAddresd.Telemedicine.Infrastructure.Middleware;

/// <summary>
/// Middleware de correlación: genera/acepta <c>X-Correlation-Id</c> y lo expone
/// en la respuesta para trazar una petición entre servicios y logs.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var existing = context.Request.Headers[HeaderName].ToString();
        var correlationId = string.IsNullOrWhiteSpace(existing)
            ? Guid.NewGuid().ToString("N")
            : existing;

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Push del correlation id al Serilog LogContext durante todo el request
        // para que cada log event (console y JSON file sink) lo lleve, y se
        // conserva el scope de ILogger existente con el contexto del request.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (logger.BeginScope("{CorrelationId}", correlationId))
        {
            await next(context);
        }
    }
}
