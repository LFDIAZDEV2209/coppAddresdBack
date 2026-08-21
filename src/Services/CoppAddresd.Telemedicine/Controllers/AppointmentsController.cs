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
/// Citas de telemedicina: agendamiento directo del profesional, agenda
/// (dashboard/calendario), cancelación y reprogramación. Los actores salen del
/// JWT; el estado de la cita y el de la sesión/sala son independientes.
/// </summary>
[ApiController]
[Route("api/v1/telemedicine/appointments")]
[Authorize]
public class AppointmentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission(TelemedicinePermissionCodes.AppointmentsSchedule)]
    [ProducesResponseType(typeof(TelemedicineAppointmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TelemedicineAppointmentDto>> Schedule(
        [FromBody] ScheduleTelemedicineAppointmentDto request,
        CancellationToken ct)
    {
        var command = new ScheduleTelemedicineAppointmentCommand(
            request.PatientId,
            request.ProfessionalId,
            request.SpecialtyId,
            request.OrganizationId,
            request.ClinicId,
            request.LocationId,
            request.ScheduledStart,
            request.DurationMinutes,
            CurrentUserId());

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(TelemedicinePermissionCodes.AppointmentsView)]
    [ProducesResponseType(typeof(TelemedicineAppointmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TelemedicineAppointmentDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetTelemedicineAppointmentQuery(id), ct));

    /// <summary>Agenda del profesional en un rango (dashboard "Mi agenda" / calendario).</summary>
    [HttpGet("agenda")]
    [RequirePermission(TelemedicinePermissionCodes.AgendaView)]
    [ProducesResponseType(typeof(IReadOnlyList<TelemedicineAppointmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TelemedicineAppointmentDto>>> Agenda(
        [FromQuery] Guid professionalId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
        => Ok(await mediator.Send(new GetProfessionalAgendaQuery(professionalId, from, to), ct));

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(TelemedicinePermissionCodes.AppointmentsCancel)]
    [ProducesResponseType(typeof(TelemedicineAppointmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TelemedicineAppointmentDto>> Cancel(
        Guid id,
        [FromBody] CancelTelemedicineAppointmentDto request,
        CancellationToken ct)
    {
        var command = new CancelTelemedicineAppointmentCommand(
            id,
            request.Reason,
            request.CancelledBy,
            CurrentUserId());
        return Ok(await mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/reschedule")]
    [RequirePermission(TelemedicinePermissionCodes.AppointmentsReschedule)]
    [ProducesResponseType(typeof(TelemedicineAppointmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TelemedicineAppointmentDto>> Reschedule(
        Guid id,
        [FromBody] RescheduleTelemedicineAppointmentDto request,
        CancellationToken ct)
    {
        var command = new RescheduleTelemedicineAppointmentCommand(
            id,
            request.NewStart,
            request.DurationMinutes,
            request.Reason,
            request.RequestedBy,
            CurrentUserId());
        return Ok(await mediator.Send(command, ct));
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}

public sealed record ScheduleTelemedicineAppointmentDto(
    Guid PatientId,
    Guid ProfessionalId,
    Guid SpecialtyId,
    Guid OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    DateTimeOffset ScheduledStart,
    int? DurationMinutes);

public sealed record CancelTelemedicineAppointmentDto(
    string Reason,
    CancelledBy CancelledBy);

public sealed record RescheduleTelemedicineAppointmentDto(
    DateTimeOffset NewStart,
    int? DurationMinutes,
    string? Reason,
    RescheduleRequestedBy RequestedBy);
