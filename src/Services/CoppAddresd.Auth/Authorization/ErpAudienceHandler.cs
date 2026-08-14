using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoppAddresd.Auth.Authorization;

/// <summary>
/// Handler de la política ErpAudience: autoriza SOLO si el token autenticado
/// tiene aud == "erp". Sin esto, un usuario staff con acceso también a "app"
/// recibiría sus permisos ERP en un token app (que saltea el security stamp)
/// y podría invocar endpoints de administración durante ≤ 15 min tras una
/// revocación — rompiendo la garantía REQ-AUDIT-03.
/// </summary>
public class ErpAudienceHandler : AuthorizationHandler<ErpAudienceRequirement>
{
    private readonly ILogger<ErpAudienceHandler> _logger;

    public ErpAudienceHandler(ILogger<ErpAudienceHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ErpAudienceRequirement requirement)
    {
        if (context.User.HasClaim(
                claim => claim.Type == JwtRegisteredClaimNames.Aud
                         && claim.Value == requirement.Audience))
        {
            _logger.LogDebug("Audience {Audience} accepted for ERP-only endpoint", requirement.Audience);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug("Audience claim missing or not {Audience}; ERP-only endpoint denied", requirement.Audience);
        }

        return Task.CompletedTask;
    }
}
