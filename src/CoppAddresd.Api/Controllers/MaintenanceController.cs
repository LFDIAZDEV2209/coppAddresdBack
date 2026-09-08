using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Application.Features.Maintenance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Endpoints de mantenimiento del sistema. Requieren permiso
/// <c>System.AdminSettings</c> para evitar ejecución accidental.
/// </summary>
[ApiController]
[Route("api/v1/maintenance")]
[Authorize]
public class MaintenanceController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Backfill de scopes de profesional: recorre todos los profesionales con
    /// usuario y asegura que tengan el rol "Professional" con scope de clínica
    /// para cada una de sus clínicas activas. Idempotente.
    /// </summary>
    [HttpPost("backfill-professional-scopes")]
    [RequirePermission(PermissionCodes.SystemAdminSettings)]
    public async Task<ActionResult<BackfillProfessionalScopesResult>> BackfillProfessionalScopes(
        CancellationToken ct)
    {
        var grantedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var command = new BackfillProfessionalScopesCommand(
            Guid.TryParse(grantedBy, out var caller) ? caller : null);

        var result = await mediator.Send(command, ct);
        return Ok(result);
    }
}
