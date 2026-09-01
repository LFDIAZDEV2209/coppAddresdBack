using System.Security.Claims;
using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Telemedicine.Controllers;

/// <summary>
/// Solicitudes de telemedicina (el paciente pide; el profesional confirma).
/// El <c>created_by</c> y el actor de las operaciones salen del JWT, nunca del
/// cuerpo de la petición.
/// </summary>
[ApiController]
[Route("api/v1/telemedicine/requests")]
[Authorize]
public class RequestsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TelemedicineRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TelemedicineRequestDto>> Create(
        [FromBody] CreateTelemedicineRequestDto request,
        CancellationToken ct
    )
    {
        var command = new CreateTelemedicineRequestCommand(
            request.PatientId,
            request.OrganizationId,
            request.SpecialtyId,
            request.ProfessionalId,
            request.ClinicId,
            request.LocationId,
            request.PreferredStart,
            request.Reason,
            CurrentUserId(),
            HasPermission(AppointmentPermissionCodes.RequestsCreate)
        );

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AppointmentPermissionCodes.RequestsView)]
    [ProducesResponseType(typeof(TelemedicineRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TelemedicineRequestDto>> GetById(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new GetTelemedicineRequestQuery(id), ct));

    /// <summary>Solicitudes del paciente (usuario autenticado o paciente indicado).</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<TelemedicineRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TelemedicineRequestDto>>> Mine(
        [FromQuery] Guid? patientId,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new GetMyRequestsQuery(
                    patientId,
                    CurrentUserId(),
                    HasPermission(AppointmentPermissionCodes.RequestsView)
                ),
                ct
            )
        );

    /// <summary>Confirma una solicitud pendiente → crea la cita Confirmed.</summary>
    [HttpPost("{id:guid}/confirm")]
    [RequirePermission(AppointmentPermissionCodes.RequestsConfirm)]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AppointmentDto>> Confirm(
        Guid id,
        [FromBody] ConfirmTelemedicineRequestDto request,
        CancellationToken ct
    )
    {
        var command = new ConfirmTelemedicineRequestCommand(
            id,
            request.ProfessionalId,
            request.ScheduledStart,
            request.DurationMinutes,
            request.LocationId,
            CurrentUserId()
        );

        var result = await mediator.Send(command, ct);
        return CreatedAtAction("GetById", "Appointments", new { id = result.Id }, result);
    }

    /// <summary>
    /// Aprueba una solicitud pendiente (primer paso del ciclo de 2): pasa a
    /// <c>Approved</c> sin crear cita. Autorización dual en el handler: admin con
    /// <c>Appointments.AdminView</c> o el profesional asignado (identidad JWT).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(TelemedicineRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemedicineRequestDto>> Approve(
        Guid id,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new ReviewTelemedicineRequestCommand(
                    id,
                    RequestDecision.Approved,
                    Reason: null,
                    CurrentUserId(),
                    HasAdminView()
                ),
                ct
            )
        );

    /// <summary>
    /// Rechaza una solicitud (Pending o Approved) con motivo obligatorio
    /// (máx. 500). Autorización dual: admin con <c>Appointments.AdminView</c> o
    /// el profesional asignado (identidad JWT).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(TelemedicineRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemedicineRequestDto>> Reject(
        Guid id,
        [FromBody] RejectTelemedicineRequestDto request,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new ReviewTelemedicineRequestCommand(
                    id,
                    RequestDecision.Rejected,
                    request.Reason,
                    CurrentUserId(),
                    HasAdminView()
                ),
                ct
            )
        );

    private bool HasPermission(string permissionCode) =>
        User.HasClaim("permission", permissionCode);

    private bool HasAdminView() =>
        User.HasClaim("permission", AppointmentPermissionCodes.AdminView);

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}

public sealed record CreateTelemedicineRequestDto(
    Guid PatientId,
    Guid OrganizationId,
    Guid SpecialtyId,
    Guid? ProfessionalId,
    Guid? ClinicId,
    Guid? LocationId,
    DateTimeOffset? PreferredStart,
    string Reason
);

public sealed record ConfirmTelemedicineRequestDto(
    Guid ProfessionalId,
    DateTimeOffset ScheduledStart,
    int? DurationMinutes,
    Guid? LocationId
);

/// <summary>Motivo del rechazo de una solicitud (obligatorio, máx. 500).</summary>
public sealed record RejectTelemedicineRequestDto(string Reason);
