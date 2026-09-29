using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Api.Authorization;

/// <summary>
/// Exige que el JWT autenticado pertenezca a una audiencia concreta
/// (claim <c>aud</c>): <c>app</c> (paciente) o <c>erp</c> (staff). Defensa en
/// profundidad del enrutamiento por el catch-all YARP del Gateway (D8): la
/// política de audiencia vive en el API, no solo en el Gateway.
/// </summary>
public class AudienceRequirement(string audience) : IAuthorizationRequirement
{
    public string Audience { get; } = audience;
}

public class AudienceAuthorizationHandler : AuthorizationHandler<AudienceRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AudienceRequirement requirement
    )
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.HasClaim("aud", requirement.Audience))
        {
            context.Succeed(requirement);
        }

        // Sin éxito: la política falla → 403 (nunca 404; el recurso es el
        // endpoint, no una alerta).
        return Task.CompletedTask;
    }
}

/// <summary>Políticas de audiencia del módulo SOS (registradas en ConfigureJwtAuthentication).</summary>
public static class SosPolicies
{
    /// <summary>Paciente autenticado de la app móvil (aud=app).</summary>
    public const string AppPatient = "SosAppPatient";

    /// <summary>Staff autenticado del ERP (aud=erp). El permiso <c>Sos.Alerts.Manage</c> y el scope clínico se validan en el endpoint.</summary>
    public const string ErpStaff = "SosErpStaff";
}
