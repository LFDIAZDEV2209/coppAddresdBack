using System.Security.Claims;

namespace CoppAddresd.Telemedicine.Infrastructure.Persistence;

/// <summary>
/// Contexto del actor para la auditoría de PostgreSQL del microservicio:
/// resuelve usuario/rol/correlación desde el JWT y el HttpContext de la petición
/// actual (scoped). El <see cref="AuditTriggerInterceptor"/> lo propaga a los
/// GUC transaccionales (<c>audit.*</c>) que lee el trigger
/// <c>audit.audit_trigger_function</c>. Sin petición autenticada (webhooks,
/// jobs), el actor es <c>SYSTEM</c> — nunca se inventa una identidad.
/// </summary>
public sealed class HttpAuditActorContext(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>Usuario autenticado del JWT (NameIdentifier) o null.</summary>
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserEmail
        => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>Primer rol del JWT (trazabilidad; los roles son múltiples).</summary>
    public string? UserRole
        => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;

    public string ActorType
        => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true
            ? "USER"
            : "SYSTEM";

    public string? IpAddress
        => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <summary>El TraceIdentifier del middleware ES el correlation id (X-Correlation-Id).</summary>
    public string? RequestId
        => httpContextAccessor.HttpContext?.TraceIdentifier;

    public string? CorrelationId
        => httpContextAccessor.HttpContext?.TraceIdentifier;
}