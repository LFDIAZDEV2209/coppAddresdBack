using System.Security.Claims;
using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Authorization;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Telemedicine.Controllers;

/// <summary>
/// Citas de telemedicina: agendamiento directo del profesional, agenda
/// (dashboard/calendario), cancelación y reprogramación. Los actores salen del
/// JWT; el estado de la cita y el de la sesión/sala son independientes.
/// </summary>
[ApiController]
[Route("api/v1/appointments")]
[Authorize]
public class AppointmentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission(AppointmentPermissionCodes.AppointmentsSchedule)]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AppointmentDto>> Schedule(
        [FromBody] ScheduleAppointmentDto request,
        CancellationToken ct
    )
    {
        var command = new ScheduleAppointmentCommand(
            request.PatientId,
            request.ProfessionalId,
            request.SpecialtyId,
            request.OrganizationId,
            request.ClinicId,
            request.LocationId,
            request.ScheduledStart,
            request.DurationMinutes,
            CurrentUserId()
        );

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AppointmentPermissionCodes.AppointmentsView)]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AppointmentDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAppointmentQuery(id), ct));

    /// <summary>
    /// Citas del paciente autenticado (app móvil), por identidad del JWT.
    /// Sin permiso ERP: la autorización la resuelve el handler.
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(PaginatedAdminAppointmentsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginatedAdminAppointmentsResult>> Mine(
        [FromQuery] AppointmentStatus? status = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        Ok(
            await mediator.Send(
                new GetMyAppointmentsQuery(CurrentUserId(), status, from, to, page, pageSize),
                ct
            )
        );

    /// <summary>Agenda del profesional en un rango (dashboard "Mi agenda" / calendario).</summary>
    [HttpGet("agenda")]
    [RequirePermission(AppointmentPermissionCodes.AgendaView)]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> Agenda(
        [FromQuery] Guid professionalId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct
    ) => Ok(await mediator.Send(new GetProfessionalAgendaQuery(professionalId, from, to), ct));

    /// <summary>
    /// Ranuras disponibles de un día o de un rango: por profesional concreto o
    /// por especialidad (el modo specialty agrega los slots de los
    /// profesionales elegibles con su conteo de libres, sin asignar ni
    /// reservar). Exactamente un modo: rango (par <c>from</c>/<c>to</c>,
    /// inclusive, máx. 14 días → <c>AvailabilityRangeResult</c>) o un día
    /// (<c>date</c> → <c>AvailabilitySlotsResult</c>).
    /// Autorización dual sin permiso específico: paciente (`aud: app`) y ERP
    /// (`aud: erp`). Tiempos en UTC (`timezoneOffset: "+00:00"`).
    /// </summary>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(AvailabilitySlotsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AvailabilityRangeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Availability(
        [FromQuery] Guid? professionalId,
        [FromQuery] Guid? specialtyId,
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? clinicId,
        [FromQuery] Guid? locationId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct
    )
    {
        if (from.HasValue || to.HasValue)
        {
            if (!from.HasValue || !to.HasValue || date.HasValue)
                throw new DomainValidationException(
                    "El modo rango requiere exactamente 'from' y 'to' (sin 'date')."
                );
            return Ok(
                await mediator.Send(
                    new GetAvailabilityRangeSlotsQuery(
                        professionalId,
                        specialtyId,
                        organizationId,
                        clinicId,
                        locationId,
                        from.Value,
                        to.Value
                    ),
                    ct
                )
            );
        }

        if (!date.HasValue)
            throw new DomainValidationException(
                "Se requiere 'date' (un día) o el par 'from'/'to' (rango)."
            );
        return Ok(
            await mediator.Send(
                new GetAvailabilitySlotsQuery(
                    professionalId,
                    specialtyId,
                    organizationId,
                    clinicId,
                    locationId,
                    date.Value
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Profesionales de una especialidad con al menos una ranura libre dentro
    /// de una ventana de días (máx. 31). Alimenta el badge "Con cupo" del
    /// picker de profesional de la app, que vive antes de elegir fecha.
    /// Autorización dual sin permiso específico: paciente (`aud: app`) y ERP.
    /// Ventana por defecto: hoy + 13 días. Tiempos en UTC.
    /// </summary>
    [HttpGet("availability/professionals")]
    [ProducesResponseType(typeof(AvailableProfessionalsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvailableProfessionalsResult>> AvailabilityProfessionals(
        [FromQuery] Guid specialtyId,
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? clinicId,
        [FromQuery] Guid? locationId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct
    )
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var end = to ?? start.AddDays(13);
        return Ok(
            await mediator.Send(
                new GetAvailableProfessionalsQuery(
                    specialtyId,
                    organizationId,
                    clinicId,
                    locationId,
                    start,
                    end
                ),
                ct
            )
        );
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AppointmentDto>> Cancel(
        Guid id,
        [FromBody] CancelAppointmentDto request,
        CancellationToken ct
    )
    {
        // Alcance dual: con el permiso ERP el actor sale del body (comportamiento
        // actual); sin permiso, el llamador es el paciente de la cita (identidad
        // del JWT) y el handler valida propiedad/estado.
        var erpMode = User.HasClaim("permission", AppointmentPermissionCodes.AppointmentsCancel);
        var command = new CancelAppointmentCommand(
            id,
            request.Reason,
            request.CancelledBy,
            CurrentUserId(),
            erpMode ? null : CurrentUserId()
        );
        return Ok(await mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/reschedule")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AppointmentDto>> Reschedule(
        Guid id,
        [FromBody] RescheduleAppointmentDto request,
        CancellationToken ct
    )
    {
        // Alcance dual (igual que Cancel): con el permiso ERP el RequestedBy
        // sale del body; sin permiso, el llamador es el paciente de la cita
        // (identidad del JWT), el servidor fuerza RequestedBy = Patient y el
        // handler valida propiedad/estado/límites.
        var erpMode = User.HasClaim(
            "permission",
            AppointmentPermissionCodes.AppointmentsReschedule
        );
        var command = new RescheduleAppointmentCommand(
            id,
            request.NewStart,
            request.DurationMinutes,
            request.Reason,
            erpMode ? request.RequestedBy : RescheduleRequestedBy.Patient,
            CurrentUserId(),
            erpMode ? null : CurrentUserId()
        );
        return Ok(await mediator.Send(command, ct));
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}

public sealed record ScheduleAppointmentDto(
    Guid PatientId,
    Guid ProfessionalId,
    Guid SpecialtyId,
    Guid OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    DateTimeOffset ScheduledStart,
    int? DurationMinutes
);

public sealed record CancelAppointmentDto(string Reason, CancelledBy CancelledBy);

public sealed record RescheduleAppointmentDto(
    DateTimeOffset NewStart,
    int? DurationMinutes,
    string? Reason,
    RescheduleRequestedBy RequestedBy
);
