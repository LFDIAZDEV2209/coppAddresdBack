using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Catalog;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Tests del paciente: pendientes, en curso y completados (shape TestMeta
    /// de la app móvil). Los completados se conservan para marcarlos como
    /// realizados; no se incluyen cancelled/expired.
    /// </summary>
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
        return Ok(visible.Select(HealthTestAssignmentDto.FromEntity).ToList());
    }

    /// <summary>Baterías asignadas al paciente (con progreso por test).</summary>
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
