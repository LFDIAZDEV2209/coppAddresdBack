using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    [RequirePermission(AppointmentPermissionCodes.RequestsCreate)]
    [ProducesResponseType(typeof(TelemedicineRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TelemedicineRequestDto>> Create(
        [FromBody] CreateTelemedicineRequestDto request,
        CancellationToken ct)
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
            CurrentUserId());

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AppointmentPermissionCodes.RequestsView)]
    [ProducesResponseType(typeof(TelemedicineRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TelemedicineRequestDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetTelemedicineRequestQuery(id), ct));

    /// <summary>Solicitudes del paciente (usuario autenticado o paciente indicado).</summary>
    [HttpGet("mine")]
    [RequirePermission(AppointmentPermissionCodes.RequestsView)]
    [ProducesResponseType(typeof(IReadOnlyList<TelemedicineRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TelemedicineRequestDto>>> Mine(
        [FromQuery] Guid? patientId,
        CancellationToken ct)
        => Ok(await mediator.Send(new GetMyRequestsQuery(patientId ?? CurrentUserId()), ct));

    /// <summary>Confirma una solicitud pendiente → crea la cita Confirmed.</summary>
    [HttpPost("{id:guid}/confirm")]
    [RequirePermission(AppointmentPermissionCodes.RequestsConfirm)]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AppointmentDto>> Confirm(
        Guid id,
        [FromBody] ConfirmTelemedicineRequestDto request,
        CancellationToken ct)
    {
        var command = new ConfirmTelemedicineRequestCommand(
            id,
            request.ProfessionalId,
            request.ScheduledStart,
            request.DurationMinutes,
            request.LocationId,
            CurrentUserId());

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(
            "GetById",
            "Appointments",
            new { id = result.Id },
            result);
    }

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
    string Reason);

public sealed record ConfirmTelemedicineRequestDto(
    Guid ProfessionalId,
    DateTimeOffset ScheduledStart,
    int? DurationMinutes,
    Guid? LocationId);
