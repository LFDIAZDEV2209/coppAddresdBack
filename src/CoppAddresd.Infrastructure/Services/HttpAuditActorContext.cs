using System.Diagnostics;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Contexto del actor extraído del HttpContext y del Activity de diagnóstico.
/// Sin Identity: ActorType = System y UserId = null. Cuando exista autenticación,
/// esta clase mapeará los claims del usuario.
/// </summary>
public sealed class HttpAuditActorContext(IHttpContextAccessor httpContextAccessor) : IAuditActorContext
{
    public AuditActorType ActorType => AuditActorType.System;

    public Guid? UserId => null;

    public string? UserEmail => null;

    public string? UserRole => null;

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? RequestId => httpContextAccessor.HttpContext?.TraceIdentifier;

    public string? CorrelationId =>
        Activity.Current?.Id ?? Activity.Current?.TraceId.ToString();
}
