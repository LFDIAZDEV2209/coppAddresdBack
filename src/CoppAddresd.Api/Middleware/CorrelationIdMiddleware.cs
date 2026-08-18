namespace CoppAddresd.Api.Middleware;

/// <summary>
/// Asigna un correlation ID a cada petición (lee <c>X-Correlation-ID</c> del
/// cliente o genera uno nuevo), lo expone en la respuesta y lo agrega al scope
/// de logging para correlacionar logs, excepciones y traces.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.ToString(),
            ["RequestMethod"] = context.Request.Method,
        }))
        {
            await next(context);
        }
    }
}
