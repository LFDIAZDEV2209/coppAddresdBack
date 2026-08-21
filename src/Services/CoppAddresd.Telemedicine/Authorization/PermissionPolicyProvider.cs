using CoppAddresd.Telemedicine.Application.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Authorization;

/// <summary>
/// Proveedor de políticas dinámicas (espejo del backend): si el nombre de la
/// política es un código de permiso conocido, se construye con el requisito
/// <see cref="PermissionRequirement"/>; el resto se delega al proveedor por
/// defecto. Permite usar <c>[RequirePermission("Telemedicine.*")]</c> sin
/// registrar cada política. Sin fallback global: los endpoints sin
/// <c>[Authorize]</c> quedan anónimos (p. ej. <c>/health</c>, webhooks).
/// </summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (TelemedicinePermissionCodes.All.Contains(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
