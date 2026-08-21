using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Authorization;
using CoppAddresd.Telemedicine.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoppAddresd.Telemedicine.Controllers;

/// <summary>
/// Vista administrativa global de Telemedicina (requiere
/// <c>Telemedicine.AdminView</c>, solo roles administrativos): KPIs, listados de
/// citas, solicitudes y sesiones. Los profesionales de línea usan su agenda por
/// identidad (AppointmentsController/RequestsController), no estos endpoints.
/// </summary>
[ApiController]
[Route("api/v1/telemedicine/admin")]
[Authorize]
[RequirePermission(TelemedicinePermissionCodes.AdminView)]
public class AdminController(IMediator mediator) : ControllerBase
{
    /// <summary>KPIs del dashboard administrativo (resumen operativo).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AdminSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetAdminSummaryQuery(ActiveClinicId()), ct));

    /// <summary>Listado global de citas con filtros (profesional, paciente, clínica, sede, estado, rango).</summary>
    [HttpGet("appointments")]
    [ProducesResponseType(typeof(PaginatedAdminAppointmentsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedAdminAppointmentsResult>> Appointments(
        [FromQuery] Guid? professionalId = null,
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? clinicId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] AppointmentStatus? status = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(
            new ListAdminAppointmentsQuery(
                professionalId, patientId, clinicId, locationId, status, from, to, page, pageSize), ct));

    /// <summary>Listado global de solicitudes con filtros (estado, profesional, paciente, rango).</summary>
    [HttpGet("requests")]
    [ProducesResponseType(typeof(PaginatedAdminRequestsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedAdminRequestsResult>> Requests(
        [FromQuery] AppointmentRequestStatus? status = null,
        [FromQuery] Guid? professionalId = null,
        [FromQuery] Guid? patientId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(
            new ListAdminRequestsQuery(status, professionalId, patientId, from, to, page, pageSize), ct));

    /// <summary>Listado global de sesiones de video (con cita, paciente y profesional).</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(PaginatedAdminSessionsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedAdminSessionsResult>> Sessions(
        [FromQuery] Guid? appointmentId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(
            new ListAdminSessionsQuery(appointmentId, from, to, page, pageSize), ct));

    private Guid? ActiveClinicId()
    {
        var value = User.FindFirst("clinic_id")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

/// <summary>
/// Contexto del usuario autenticado para la UI: resuelve el profesional y/o el
/// paciente del usuario del JWT. Es el "me" de Telemedicina: el frontend lo usa
/// para saber si el usuario es profesional (y su id) o paciente, y así cargar
/// "mi agenda"/"mis solicitudes" sin adivinar ids.
/// </summary>
[ApiController]
[Route("api/v1/telemedicine/me")]
[Authorize]
public class MeController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CurrentUserContextDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserContextDto>> Get(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCurrentUserContextQuery(CurrentUserId()), ct);
        return Ok(result);
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
