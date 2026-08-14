using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Auth.Authorization;

/// <summary>
/// Restringe un controller/action a tokens con aud == "erp" (REQ-AUDIT-03).
/// Aplicado a nivel de clase en UsersController, RolesController y
/// PermissionsController; [AllowAnonymous] en una acción la excluye de TODA
/// autorización (comportamiento estándar de ASP.NET Core).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireErpAudienceAttribute : AuthorizeAttribute
{
    public RequireErpAudienceAttribute()
        : base(policy: ErpAudienceRequirement.PolicyName)
    {
    }
}
