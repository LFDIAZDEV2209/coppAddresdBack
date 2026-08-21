using System.Security.Claims;
using CoppAddresd.Telemedicine.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Telemedicine.Authorization;

/// <summary>
/// Autorización de permisos del micro: evalúa primero el claim <c>permission</c>
/// del access token (permisos globales, cero consultas en el hot path); si no
/// está presente, consulta al Auth Service con la cadena de scopes del contexto
/// activo (header <c>X-Clinic-Id</c> → Clinic + Global). Así los profesionales
/// con roles asignados por clínica (scoped) pueden operar su agenda y citas sin
/// necesitar permisos globales en el JWT.
/// </summary>
public class PermissionHandler(
    ITelemedicineScopedAuthorizationClient scopedClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<PermissionHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    private const string PermissionClaimType = "permission";
    private const string SecurityStampClaim = "security_stamp";
    private const string ClinicIdHeader = "X-Clinic-Id";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // 1. Permiso global (claims JWT): aplica en cualquier contexto.
        if (context.User.HasClaim(PermissionClaimType, requirement.PermissionCode))
        {
            logger.LogDebug("Permiso {Permission} concedido por claims", requirement.PermissionCode);
            context.Succeed(requirement);
            return;
        }

        // 2. Permiso scoped: introspección al Auth Service con el contexto
        //    activo de la petición (clínica del header, si existe).
        if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userIdValue
            && Guid.TryParse(userIdValue, out var userId))
        {
            var chain = BuildScopeChain();
            var securityStamp = context.User.FindFirstValue(SecurityStampClaim) ?? string.Empty;

            if (await scopedClient.AuthorizeAsync(
                    userId, securityStamp, requirement.PermissionCode, chain,
                    httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None))
            {
                logger.LogDebug("Permiso {Permission} concedido por introspección scoped ({Chain})",
                    requirement.PermissionCode, ScopeEntry.EncodeChain(chain));
                context.Succeed(requirement);
                return;
            }
        }

        logger.LogDebug("Permiso {Permission} denegado", requirement.PermissionCode);
    }

    private IReadOnlyList<ScopeEntry> BuildScopeChain()
    {
        var clinicIdValue = httpContextAccessor.HttpContext?.Request.Headers[ClinicIdHeader].ToString();
        if (Guid.TryParse(clinicIdValue, out var clinicId))
        {
            return [new ScopeEntry("Clinic", clinicId), ScopeEntry.Global];
        }

        return [ScopeEntry.Global];
    }
}