using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Professionals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Perfil autogestionado del usuario actual (wizard de onboarding y edición
/// propia). Solo autenticación: el profesional completa sus datos sin requerir
/// el permiso administrativo Employees.Update.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MyProfileController(
    IMediator mediator,
    ICurrentContext current) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<EmployeeDto>> GetProfile(CancellationToken ct)
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var profile = await mediator.Send(new GetMyProfileQuery(userId), ct);
        if (profile is null)
        {
            return NotFound(new { message = "No hay un perfil vinculado a tu cuenta." });
        }

        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<EmployeeDto>> UpdateProfile(
        [FromBody] UpdateMyProfileRequest request,
        CancellationToken ct)
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var command = new UpdateMyProfileCommand(
            userId,
            request.ProfessionalTypeId,
            request.Bio,
            request.PhotoStorageKey,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.SpecialtyIds,
            request.Licenses,
            request.CompleteOnboarding);

        var updated = await mediator.Send(command, ct);
        return Ok(updated);
    }
}

public record UpdateMyProfileRequest(
    Guid? ProfessionalTypeId,
    string? Bio,
    string? PhotoStorageKey,
    string? PhoneCountryCode,
    string? PhoneNumber,
    IReadOnlyList<Guid>? SpecialtyIds,
    IReadOnlyList<LicenseInput>? Licenses,
    bool CompleteOnboarding = false);
