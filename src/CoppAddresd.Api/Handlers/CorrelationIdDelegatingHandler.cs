using Microsoft.AspNetCore.Http;

namespace CoppAddresd.Api.Handlers;

/// <summary>
/// Propaga el correlation ID del request del frontend hacia los servicios
/// internos (AI Service, Auth...) para correlacionar logs a través de la
/// cadena Frontend → Backend → AI. Si el request entrante no trae
/// X-Correlation-ID, el middleware ya generó uno en HttpContext.Items.
/// </summary>
public sealed class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId =
            httpContextAccessor.HttpContext?.Items["CorrelationId"] as string;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
