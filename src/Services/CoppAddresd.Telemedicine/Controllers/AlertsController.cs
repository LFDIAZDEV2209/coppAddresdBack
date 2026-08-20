using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoppAddresd.Telemedicine.Controllers;

/// <summary>
/// Bandeja de alertas. La autorización se resuelve en el handler a partir del
/// JWT: el profesional (sin <c>Telemedicine.AlertsView</c>) accede a sus propias
/// alertas por identidad; un usuario con <c>AlertsView</c> (roles admin) accede
/// a la vista global. Por eso estos endpoints no llevan <c>[RequirePermission]</c>.
/// </summary>
[ApiController]
[Route("api/v1/telemedicine/alerts")]
[Authorize]
public class AlertsController(IMediator mediator) : ControllerBase
{
    /// <summary>Bandeja paginada (no leídas primero). <c>?unreadOnly=true</c> filtra solo las no leídas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedAlertsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginatedAlertsResult>> List(
        CancellationToken ct,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await mediator.Send(
            new ListAlertsQuery(CurrentUserId(), HasAlertsView(), unreadOnly, page, pageSize), ct));    /// <summary>Resumen de la bandeja (recuento de no leídas para el badge).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AlertsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AlertsSummaryDto>> Summary(CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListAlertsQuery(CurrentUserId(), HasAlertsView(), UnreadOnly: true, Page: 1, PageSize: 1), ct);
        return Ok(new AlertsSummaryDto(result.Unread));
    }

    /// <summary>Marca una alerta como leída.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var marked = await mediator.Send(
            new MarkAlertReadCommand(id, CurrentUserId(), HasAlertsView()), ct);
        return marked ? NoContent() : NotFound();
    }

    /// <summary>Marca todas las alertas de la bandeja como leídas. Devuelve cuántas se marcaron.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<int>> ReadAll(CancellationToken ct)
        => Ok(await mediator.Send(
            new MarkAllAlertsReadCommand(CurrentUserId(), HasAlertsView()), ct));

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private bool HasAlertsView()
        => User.HasClaim("permission", TelemedicinePermissionCodes.AlertsView);
}
