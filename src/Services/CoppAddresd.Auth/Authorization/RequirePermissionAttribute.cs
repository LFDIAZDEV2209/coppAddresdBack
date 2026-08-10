using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Auth.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionCode)
        : base(policy: permissionCode)
    {
    }
}
