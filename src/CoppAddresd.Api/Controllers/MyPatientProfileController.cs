using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Application.Features.Measurements.Queries.GetMyMetricsHistory;
using CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;
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

    /// <summary>
    /// Mediciones clínicas propias del paciente (APP móvil, self-service).
    /// El paciente se deriva SIEMPRE del JWT (<c>ICurrentContext.UserId</c> →
    /// <c>patient_profiles.user_id</c> en el handler, anti-IDOR): sin perfil
    /// vinculado → 404. Filtro opcional <c>codes</c> (CSV de códigos canónicos,
    /// case-insensitive) y paginación por cursor opaco (<c>pageSize</c> 1-100,
    /// <c>cursor</c> de la página anterior).
    /// </summary>
    [HttpGet("measurements")]
    [ProducesResponseType(typeof(CursorPagedResult<MeasurementItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CursorPagedResult<MeasurementItemDto>>> GetMyMeasurements(
        [FromQuery] int? pageSize,
        [FromQuery] string? cursor,
        [FromQuery] string? codes,
        CancellationToken ct
    )
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        string[]? metricCodes = string.IsNullOrWhiteSpace(codes)
            ? null
            : codes.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        return Ok(
            await mediator.Send(
                new GetMyMeasurementsQuery(
                    userId,
                    pageSize ?? GetMyMeasurementsQuery.DefaultPageSize,
                    cursor,
                    metricCodes
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Historial de métricas clínicas propias SIN exigir inscripción activa
    /// (APP móvil, self-service). Misma forma de respuesta que
    /// <c>GET /api/v1/program/me/metrics-history</c> (serie diaria por métrica
    /// con fecha y valor) pero sin el 404 <c>NO_ACTIVE_ENROLLMENT</c>: el
    /// paciente se deriva SIEMPRE del JWT (<c>ICurrentContext.UserId</c> →
    /// <c>patient_profiles.user_id</c> en el handler, anti-IDOR). Códigos
    /// desconocidos → 400 <c>METRICS_UNKNOWN</c>; días fuera de [7, 365] → 400.
    /// </summary>
    [HttpGet("metrics-history")]
    [ProducesResponseType(typeof(MetricsHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MetricsHistoryResponseDto>> GetMyMetricsHistory(
        [FromQuery] string? codes,
        [FromQuery] int? days,
        CancellationToken ct
    )
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var parsedCodes = string.IsNullOrWhiteSpace(codes)
            ? []
            : codes.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        return Ok(
            await mediator.Send(new GetMyMetricsHistoryQuery(userId, parsedCodes, days ?? 0), ct)
        );
    }

    /// <summary>
    /// Plan de alimentación activo del paciente (APP móvil, self-service).
    /// El paciente se deriva SIEMPRE del JWT (<c>ICurrentContext.UserId</c> →
    /// <c>patient_profiles.user_id</c> en el handler, anti-IDOR): resuelve la
    /// asignación activa del ERP y devuelve el plan con sus días y comidas.
    /// Sin perfil vinculado → 404; sin plan activo asignado → 404 con mensaje
    /// propio (no es un error: el paciente aún no tiene plan).
    /// </summary>
    [HttpGet("nutrition-plan")]
    [ProducesResponseType(typeof(MyNutritionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyNutritionPlanDto>> GetMyNutritionPlan(CancellationToken ct)
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var dto = await mediator.Send(new GetMyNutritionPlanQuery(userId), ct);
        if (dto is null)
        {
            return NotFound(new { message = "No tienes un plan de alimentación activo asignado." });
        }

        return Ok(dto);
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
