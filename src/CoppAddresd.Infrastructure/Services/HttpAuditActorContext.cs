using System.Diagnostics;
using System.Security.Claims;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Contexto del actor extraído del HttpContext y del Activity de diagnóstico.
/// Cuando existe autenticación, lee los claims JWT (nameidentifier → UserId,
/// email → UserEmail, role → UserRole). Sin autenticación, ActorType = System.
/// </summary>
public sealed class HttpAuditActorContext(IHttpContextAccessor httpContextAccessor) : IAuditActorContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public AuditActorType ActorType =>
        CoppAddresd.Application.Features.Nutrition.Cases.NutritionSystemActor.IsActive ? AuditActorType.System
            : User?.Identity?.IsAuthenticated == true ? AuditActorType.User : AuditActorType.System;

    public Guid? UserId =>
        CoppAddresd.Application.Features.Nutrition.Cases.NutritionSystemActor.IsActive ? null
            : Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserEmail => CoppAddresd.Application.Features.Nutrition.Cases.NutritionSystemActor.IsActive ? null : User?.FindFirstValue(ClaimTypes.Email);

    public string? UserRole => CoppAddresd.Application.Features.Nutrition.Cases.NutritionSystemActor.IsActive
        ? CoppAddresd.Application.Features.Nutrition.Cases.NutritionSystemActor.Name : User?.FindFirstValue(ClaimTypes.Role);

    public string? IpAddress =>
        httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? RequestId =>
        httpContextAccessor.HttpContext?.TraceIdentifier;

    public string? CorrelationId =>
        Activity.Current?.Id ?? Activity.Current?.TraceId.ToString();
}
