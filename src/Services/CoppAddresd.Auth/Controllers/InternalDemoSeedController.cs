using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Seed interno de pacientes demo para pruebas locales (clave
/// <c>X-Internal-Key</c>). Solo activo en Development: crea usuarios con
/// password conocida para que el equipo pruebe el flujo móvil (login directo
/// y OTP) sin proveedor real. Idempotente.
/// </summary>
[ApiController]
[Route("api/auth/internal")]
[AllowAnonymous]
[RequireInternalKey]
public class InternalDemoSeedController(
    IDemoPatientSeedService seedService,
    IHostEnvironment environment
) : ControllerBase
{
    [HttpPost("seed-patient-demo")]
    [ProducesResponseType(typeof(DemoPatientSeedResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DemoPatientSeedResult>> SeedPatientDemo(
        [FromBody] SeedPatientDemoRequest request,
        CancellationToken ct
    )
    {
        if (!environment.IsDevelopment())
        {
            return Forbid();
        }

        if (request.PatientId == Guid.Empty)
        {
            return BadRequest(new { message = "patientId es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { message = "password debe tener al menos 6 caracteres." });
        }

        var result = await seedService.SeedPatientAsync(
            request.PatientId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            ct
        );

        return Ok(result);
    }

    [HttpPost("seed-demo-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SeedDemoPassword(
        [FromBody] SeedDemoPasswordRequest request,
        CancellationToken ct
    )
    {
        if (!environment.IsDevelopment())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "email y password son requeridos." });
        }

        var ok = await seedService.ResetDemoPasswordAsync(request.Email, request.Password);
        if (!ok)
        {
            return NotFound(new { message = $"No existe un usuario con email '{request.Email}'." });
        }

        return Ok(new { email = request.Email, reset = true });
    }
}

/// <summary>Solicitud de seed de un paciente demo (idempotente).</summary>
public sealed record SeedPatientDemoRequest(
    Guid PatientId,
    string FirstName,
    string LastName,
    string Email,
    string Password
);

/// <summary>Solicitud de fijado de password para un usuario demo (dev).</summary>
public sealed record SeedDemoPasswordRequest(string Email, string Password);
