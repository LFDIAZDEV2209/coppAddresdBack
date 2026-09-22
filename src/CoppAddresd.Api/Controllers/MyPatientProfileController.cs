using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Patients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Perfil autogestionado del PACIENTE (APP mÃ³vil, aud=app). El perfil se
/// resolviÃ³ por JWT: sin ids en la ruta/cuerpo (anti-IDOR). La creaciÃ³n de
/// pacientes sigue viviendo en el ERP â€” la APP solo completa/edita su perfil.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MyPatientProfileController(IMediator mediator, ICurrentContext current)
    : ControllerBase
{
    [HttpGet("patient-profile")]
    public async Task<ActionResult<PatientSelfProfileDto>> GetPatientProfile(CancellationToken ct)
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var profile = await mediator.Send(new GetMyPatientProfileQuery(userId), ct);
        if (profile is null)
        {
            return NotFound(
                new { message = "No hay un perfil de paciente vinculado a tu cuenta." }
            );
        }

        return Ok(profile);
    }

    [HttpPut("patient-profile")]
    public async Task<ActionResult<PatientSelfProfileDto>> UpdatePatientProfile(
        [FromBody] UpdateMyPatientProfileRequest request,
        CancellationToken ct
    )
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var result = await mediator.Send(
            new UpdateMyPatientProfileCommand(
                userId,
                request.DateOfBirth,
                request.Email,
                request.Phone,
                request.EmergencyName,
                request.EmergencyRelationship,
                request.EmergencyPhone,
                request.EmergencyEmail,
                request.InsurerId,
                request.MemberId
            ),
            ct
        );

        if (!result.Success)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Profile);
    }
}

/// <summary>Solicitud de actualizaciÃ³n del perfil del paciente (APP mÃ³vil).</summary>
public sealed record UpdateMyPatientProfileRequest(
    DateTime? DateOfBirth,
    string? Email,
    string? Phone,
    string? EmergencyName,
    string? EmergencyRelationship,
    string? EmergencyPhone,
    string? EmergencyEmail,
    Guid? InsurerId,
    string? MemberId
);
