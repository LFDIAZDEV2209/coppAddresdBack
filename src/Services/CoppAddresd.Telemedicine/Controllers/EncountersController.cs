using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoppAddresd.Telemedicine.Controllers;

/// <summary>
/// Espacio clínico de una cita (encuentro clínico): consulta del registro,
/// guardado de borrador y finalización. La autorización se resuelve en el
/// handler a partir del JWT: solo el profesional de la cita (identidad) o un
/// supervisor con <c>Telemedicine.SessionsManage</c> acceden; el paciente NO
/// (datos clínicos sensibles).
/// </summary>
[ApiController]
[Route("api/v1/appointments/{appointmentId:guid}/encounter")]
[Authorize]
public class EncountersController(IMediator mediator) : ControllerBase
{
    /// <summary>Registro clínico de la cita (404 si aún no se creó).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ClinicalEncounterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicalEncounterDto>> Get(Guid appointmentId, CancellationToken ct)
        => Ok(await mediator.Send(
            new GetClinicalEncounterQuery(appointmentId, CurrentUserId(), HasManagePermission()), ct));

    /// <summary>Guarda el registro clínico (crea el borrador si no existe; los completados son inmutables).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ClinicalEncounterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClinicalEncounterDto>> Save(
        Guid appointmentId,
        [FromBody] SaveClinicalEncounterDto request,
        CancellationToken ct)
        => Ok(await mediator.Send(
            new SaveClinicalEncounterCommand(
                appointmentId,
                request.ClinicalData,
                request.Notes,
                CurrentUserId(),
                HasManagePermission()), ct));

    /// <summary>Finaliza el registro clínico (Draft → Completed, estado final; idempotente).</summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(ClinicalEncounterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClinicalEncounterDto>> Complete(
        Guid appointmentId,
        [FromBody] SaveClinicalEncounterDto? request,
        CancellationToken ct)
        => Ok(await mediator.Send(
            new CompleteClinicalEncounterCommand(
                appointmentId,
                request?.ClinicalData,
                request?.Notes,
                CurrentUserId(),
                HasManagePermission()), ct));

    /// <summary>
    /// Adendas del encuentro (F4): lista cronológica por <c>(created_at, id)</c>.
    /// Arreglo vacío si el encuentro aún no existe (la UI no necesita 404).
    /// Misma autorización que el encuentro (profesional/supervisor; paciente no).
    /// </summary>
    [HttpGet("addenda")]
    [ProducesResponseType(typeof(IReadOnlyList<EncounterAddendumDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EncounterAddendumDto>>> GetAddenda(
        Guid appointmentId,
        CancellationToken ct)
        => Ok(await mediator.Send(
            new GetEncounterAddendaQuery(appointmentId, CurrentUserId(), HasManagePermission()), ct));

    /// <summary>
    /// Agrega una adenda a un encuentro completado (F4, append-only): el
    /// registro original no se modifica. En borrador o sin encuentro → 409.
    /// El autor y el nombre snapshot salen del JWT.
    /// </summary>
    [HttpPost("addenda")]
    [ProducesResponseType(typeof(EncounterAddendumDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EncounterAddendumDto>> AddAddendum(
        Guid appointmentId,
        [FromBody] AddEncounterAddendumDto request,
        CancellationToken ct)
    {
        // Snapshot legible del autor desde el JWT (nombre o, si no, email).
        var authorName = User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value;

        var addendum = await mediator.Send(
            new AddEncounterAddendumCommand(
                appointmentId,
                request.Body,
                authorName,
                CurrentUserId(),
                HasManagePermission()), ct);

        return CreatedAtAction(nameof(GetAddenda), new { appointmentId }, addendum);
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private bool HasManagePermission()
        => User.HasClaim("permission", CoppAddresd.Telemedicine.Application.Constants.AppointmentPermissionCodes.SessionsManage);
}

public sealed record SaveClinicalEncounterDto(
    ClinicalDataDto? ClinicalData,
    string? Notes);

/// <summary>Cuerpo de la adenda del encuentro (F4): texto plano 1–2000.</summary>
public sealed record AddEncounterAddendumDto(string Body);
