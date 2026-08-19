using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Api.Authorization;

/// <summary>Declara el permiso requerido como política (el código ES el nombre de la política).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute(string permissionCode) : AuthorizeAttribute(policy: permissionCode)
{
    public string PermissionCode { get; } = permissionCode;
}
