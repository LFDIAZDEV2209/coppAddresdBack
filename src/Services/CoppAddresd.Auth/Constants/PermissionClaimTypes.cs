namespace CoppAddresd.Auth.Constants;

/// <summary>
/// Tipos de claim usados por la autorización basada en claims.
/// </summary>
public static class PermissionClaimTypes
{
    /// <summary>
    /// Claim que transporta un código de permiso ("Users.View", "Roles.Assign", ...).
    /// Se emite en el access token en el login/refresh (REQ-CLAIMS-01/02) y lo lee
    /// <c>PermissionHandler</c> para autorizar sin consultar la BD (REQ-CLAIMS-03).
    /// </summary>
    public const string Permission = "permission";
}
