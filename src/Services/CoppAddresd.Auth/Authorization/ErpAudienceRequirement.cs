using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Auth.Authorization;

/// <summary>
/// Requisito de audiencia ERP: los endpoints de administración del ERP
/// (Users/Roles/Permissions) solo aceptan tokens con aud == "erp" (REQ-AUDIT-03).
/// Un token "app" — que además skipea la validación de security stamp — no puede
/// invocar estos endpoints aunque lleve claims de permiso.
/// </summary>
public class ErpAudienceRequirement : IAuthorizationRequirement
{
    /// <summary>Nombre de la política registrada en <c>AuthorizationOptions</c>.</summary>
    public const string PolicyName = "ErpAudience";

    /// <summary>Audiencia requerida: código de aplicación del ERP.</summary>
    public string Audience { get; } = CoppAddresd.Auth.Constants.ApplicationCodes.Erp;
}
