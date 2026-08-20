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
[Route("api/v1/telemedicine/appointments/{appointmentId:guid}/encounter")]
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

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private bool HasManagePermission()
        => User.HasClaim("permission", CoppAddresd.Telemedicine.Application.Constants.TelemedicinePermissionCodes.SessionsManage);
}

public sealed record SaveClinicalEncounterDto(
    ClinicalDataDto? ClinicalData,
    string? Notes);
