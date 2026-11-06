using CoppAddresd.Api.Localization;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Assignments;
using CoppAddresd.Application.Features.HealthTests.Catalog;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Tests de Salud — API del paciente (Mobile). Autenticada con JWT de
/// aplicación <c>app</c>; el paciente se resuelve por
/// <c>app.patient_profiles.user_id</c> (o claim patient_id) vía
/// <see cref="IProgramActorContext"/>, NUNCA del payload. Cualquier recurso
/// ajeno responde 404 (no revela existencia).
/// </summary>
[ApiController]
[Route("api/v1/health-tests/me")]
[Authorize]
public class HealthTestsMeController(
    IMediator mediator,
    IProgramActorContext actorContext,
    IHealthTestRepository repository
) : ControllerBase
{
    // ===================== MIS ASIGNACIONES =====================

    [HttpGet("profile-analysis")]
    public Task<ActionResult<AssessmentProfileGuidance>> GetProfileAnalysis(
        [FromServices] IAssessmentProfileAnalysis analysis, CancellationToken ct, [FromQuery] string language = "es")
        => ProfileAnalysisAsync(analysis, language, false, ct);

    [HttpPost("profile-analysis")]
    [EnableRateLimiting("assessment-analysis")]
    public Task<ActionResult<AssessmentProfileGuidance>> GenerateProfileAnalysis(
        [FromServices] IAssessmentProfileAnalysis analysis, CancellationToken ct, [FromQuery] string language = "es")
        => ProfileAnalysisAsync(analysis, language, true, ct);

    private async Task<ActionResult<AssessmentProfileGuidance>> ProfileAnalysisAsync(
        IAssessmentProfileAnalysis analysis, string language, bool generate, CancellationToken ct)
    {
        if (language is not ("es" or "en")) return BadRequest(new { message = "Idioma no válido." });
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null) return NotFound();
        if (actorContext.UserId is not { } userId) return Unauthorized();
        try
        {
            var result = await analysis.GetAsync(patientId.Value, userId, language, generate, ct);
            return result is null ? NotFound(new { message = "Aún no hay un análisis disponible para estas evaluaciones." }) : Ok(result);
        }
        catch (AssessmentAnalysisUnavailableException)
        { return StatusCode(503, new { message = "Vita no pudo preparar el análisis. Tus resultados siguen disponibles; intenta de nuevo." }); }
    }

    /// <summary>
    /// Tests del paciente: pendientes, en curso y completados (shape TestMeta
    /// de la app móvil). Los completados se conservan para marcarlos como
    /// realizados; no se incluyen cancelled/expired.
    /// </summary>
    [LocalizePatientContent(PatientContentProfile.AssessmentAssignments)]
    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<HealthTestAssignmentDto>>> GetMyAssignments(
        CancellationToken ct
    )
    {
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var assignments = await repository.ListAssignmentsByPatientAsync(patientId.Value, ct);
        var visible = assignments
            .Where(a =>
                a.Status
                    is HealthTestAssignmentStatus.pending
                        or HealthTestAssignmentStatus.in_progress
                        or HealthTestAssignmentStatus.completed
            )
            .ToList();

        // Lazy fallback (decisión FASE 2: ERP + lazy): si el paciente NO tiene
        // ninguna asignación visible, se auto-asigna la batería inicial
        // (idempotente: no duplica si ya hay pendiente/in-progress).
        if (visible.Count == 0)
        {
            await mediator.Send(new AutoAssignInitialBatteryCommand(patientId.Value), ct);
            assignments = await repository.ListAssignmentsByPatientAsync(patientId.Value, ct);
            visible = assignments
                .Where(a =>
                    a.Status
                        is HealthTestAssignmentStatus.pending
                            or HealthTestAssignmentStatus.in_progress
                            or HealthTestAssignmentStatus.completed
                )
                .ToList();
        }

        return Ok(visible.Select(HealthTestAssignmentDto.FromEntity).ToList());
    }

    /// <summary>Baterías asignadas al paciente (con progreso por test).</summary>
    [LocalizePatientContent(PatientContentProfile.AssessmentBatteries)]
    [HttpGet("batteries")]
    public async Task<ActionResult<IReadOnlyList<HealthTestBatteryAssignmentDto>>> GetMyBatteries(
        CancellationToken ct
    )
    {
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var batteries = await repository.ListBatteryAssignmentsByPatientAsync(patientId.Value, ct);
        return Ok(batteries.Select(HealthTestBatteryAssignmentDto.FromEntity).ToList());
    }

    /// <summary>Preguntas de la versión de una asignación (shape ScaleQ de la app móvil).</summary>
    [LocalizePatientContent(PatientContentProfile.AssessmentDetail)]
    [HttpGet("tests/{assignmentId:guid}")]
    public async Task<ActionResult<HealthTestVersionDetailDto>> GetMyTest(
        Guid assignmentId,
        CancellationToken ct
    )
    {
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var assignment = await repository.GetAssignmentByIdAsync(assignmentId, ct);
        if (assignment is null || assignment.PatientId != patientId.Value)
        {
            // 404 (no revela la existencia del recurso ajeno).
            return NotFound(new { message = "Test no encontrado" });
        }

        var version = await mediator.Send(new GetVersionQuery(assignment.VersionId), ct);
        return version is null ? NotFound(new { message = "Test no encontrado" }) : Ok(version);
    }

    // ===================== INICIO Y ENVÍO =====================

    [HttpPost("tests/{assignmentId:guid}/start")]
    public async Task<ActionResult<HealthTestEvaluationDto>> StartTest(
        Guid assignmentId,
        CancellationToken ct
    )
    {
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var assignment = await repository.GetAssignmentByIdAsync(assignmentId, ct);
        if (assignment is null || assignment.PatientId != patientId.Value)
        {
            return NotFound(new { message = "Test no encontrado" });
        }

        return Ok(await mediator.Send(new StartEvaluationCommand(assignmentId), ct));
    }

    [HttpPost("tests/{assignmentId:guid}/submit")]
    public async Task<ActionResult<HealthTestEvaluationDto>> SubmitTest(
        Guid assignmentId,
        [FromBody] SubmitEvaluationRequest request,
        CancellationToken ct
    )
    {
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var assignment = await repository.GetAssignmentByIdAsync(assignmentId, ct);
        if (assignment is null || assignment.PatientId != patientId.Value)
        {
            return NotFound(new { message = "Test no encontrado" });
        }

        return Ok(await mediator.Send(new SubmitEvaluationCommand(assignmentId, request), ct));
    }

    // ===================== RESULTADOS / HISTORIAL =====================

    /// <summary>Resumen de scores por dimensión + tipificación (derivación determinista).</summary>
    [LocalizePatientContent(PatientContentProfile.AssessmentResults)]
    [HttpGet("results")]
    public async Task<ActionResult<IReadOnlyList<HealthTestResultDto>>> GetMyResults(
        CancellationToken ct
    )
    {
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var results = await repository.ListResultsByPatientAsync(patientId.Value, ct);
        return Ok(results.Select(HealthTestResultDto.FromEntity).ToList());
    }

    /// <summary>Historial de evaluaciones del paciente (para evolución).</summary>
    [LocalizePatientContent(PatientContentProfile.AssessmentHistory)]
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<HealthTestEvaluationDto>>> GetMyHistory(
        CancellationToken ct
    )
    {
        var patientId = await ResolvePatientOr404Async(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var evaluations = await repository.ListEvaluationsByPatientAsync(patientId.Value, ct);
        return Ok(evaluations.Select(HealthTestEvaluationDto.FromEntity).ToList());
    }

    // ===================== HELPERS =====================

    /// <summary>
    /// Resuelve el id del paciente desde el JWT (patient_id claim o
    /// app.patient_profiles.user_id). Nunca del payload.
    /// </summary>
    private async Task<Guid?> ResolvePatientOr404Async(CancellationToken ct) =>
        await actorContext.ResolvePatientProfileIdAsync(ct);
}
