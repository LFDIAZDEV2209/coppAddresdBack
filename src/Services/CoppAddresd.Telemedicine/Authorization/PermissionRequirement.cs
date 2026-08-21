using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Telemedicine.Authorization;

/// <summary>Requisito de autorización: el usuario debe portar el claim <c>permission</c> con el código pedido.</summary>
public class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
