namespace CoppAddresd.Api.Constants;

/// <summary>
/// Claim que transporta un código de permiso en el JWT emitido por el Auth
/// Service ("Users.View", "Patients.Create", ...). La API lo lee en
/// <c>PermissionHandler</c> para autorizar sin consultar la BD.
/// </summary>
public static class PermissionClaimTypes
{
    public const string Permission = "permission";
}
