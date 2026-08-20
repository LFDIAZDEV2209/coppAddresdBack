using CoppAddresd.Api.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Authorization;

/// <summary>
/// Resuelve dinámicamente una política por código de permiso (el código es el
/// nombre de la política), espejo del Auth Service. Permite usar
/// <c>[RequirePermission("Patients.View")]</c> sin registrar cada política.
/// </summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (IsPermissionCode(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    private static bool IsPermissionCode(string policyName)
        => PermissionCodes.All.Contains(policyName);
}
