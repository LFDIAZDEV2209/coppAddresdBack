using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Api.Authorization;

/// <summary>Requiere que el JWT contenga el claim <c>permission</c> con el código indicado.</summary>
public class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode
        ?? throw new ArgumentNullException(nameof(permissionCode));
}
