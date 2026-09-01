using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Alerts;
using CoppAddresd.Application.Features.HealthTests.Assignments;
using CoppAddresd.Application.Features.HealthTests.Catalog;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Tests de Salud — administración y monitoreo del ERP. Permisos:
/// <c>HealthTests.Manage</c> (catálogo), <c>HealthTests.Assign</c> (asignación),
/// <c>HealthTests.Review</c> (alertas), <c>HealthTests.View</c> (global) o
/// <c>HealthTests.ViewOwn</c> (solo pacientes asignados al profesional del JWT).
/// </summary>
[ApiController]
[Route("api/v1/health-tests")]
[Authorize]
public class HealthTestsController(
    IMediator mediator,
    ICurrentContext context,
    IHealthTestRepository repository
) : ControllerBase
{
    // ===================== CATÁLOGO =====================

    [HttpGet]
    [RequirePermission(PermissionCodes.HealthTestsView)]
    public async Task<
        ActionResult<PaginatedHealthTestsResult<HealthTestInstrumentDto>>
    > ListInstruments(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        Ok(
            await mediator.Send(
                new ListInstrumentsQuery(search, category, isActive, page, pageSize),
                ct
            )
        );

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.HealthTestsView)]
    public async Task<ActionResult<HealthTestInstrumentDto>> GetInstrument(
        Guid id,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new GetInstrumentQuery(id), ct);
        return result is null
            ? NotFound(new { message = "Instrumento no encontrado" })
            : Ok(result);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.HealthTestsManage)]
    public async Task<ActionResult<HealthTestInstrumentDto>> CreateInstrument(
        [FromBody] CreateInstrumentRequest request,
        CancellationToken ct
    ) => Ok(await mediator.Send(new CreateInstrumentCommand(request, context.UserId), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.HealthTestsManage)]
    public async Task<ActionResult<HealthTestInstrumentDto>> UpdateInstrument(
        Guid id,
        [FromBody] UpdateInstrumentRequest request,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new UpdateInstrumentCommand(id, request), ct);
        return result is null
            ? NotFound(new { message = "Instrumento no encontrado" })
            : Ok(result);
    }

    // ===================== VERSIONES =====================

    [HttpGet("{id:guid}/versions")]
    [RequirePermission(PermissionCodes.HealthTestsView)]
    public async Task<ActionResult<IReadOnlyList<HealthTestVersionDto>>> ListVersions(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new ListVersionsQuery(id), ct));

    [HttpGet("versions/{versionId:guid}")]
    [RequirePermission(PermissionCodes.HealthTestsView)]
    public async Task<ActionResult<HealthTestVersionDetailDto>> GetVersion(
        Guid versionId,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new GetVersionQuery(versionId), ct);
        return result is null ? NotFound(new { message = "Versión no encontrada" }) : Ok(result);
    }

    [HttpPost("{id:guid}/versions")]
    [RequirePermission(PermissionCodes.HealthTestsManage)]
    public async Task<ActionResult<HealthTestVersionDto>> CreateVersion(
        Guid id,
        [FromBody] CreateVersionRequest request,
        CancellationToken ct
    ) => Ok(await mediator.Send(new CreateVersionCommand(id, request), ct));

    [HttpPost("versions/{versionId:guid}/publish")]
    [RequirePermission(PermissionCodes.HealthTestsManage)]
    public async Task<ActionResult<HealthTestVersionDto>> PublishVersion(
        Guid versionId,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new PublishVersionCommand(versionId), ct);
        return result is null ? NotFound(new { message = "Versión no encontrada" }) : Ok(result);
    }

    [HttpPost("versions/{versionId:guid}/retire")]
    [RequirePermission(PermissionCodes.HealthTestsManage)]
    public async Task<ActionResult<HealthTestVersionDto>> RetireVersion(
        Guid versionId,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new RetireVersionCommand(versionId), ct);
        return result is null ? NotFound(new { message = "Versión no encontrada" }) : Ok(result);
    }

    [HttpPost("versions/{versionId:guid}/clone")]
    [RequirePermission(PermissionCodes.HealthTestsManage)]
    public async Task<ActionResult<HealthTestVersionDto>> CloneVersion(
        Guid versionId,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new CloneVersionCommand(versionId), ct);
        return result is null
            ? NotFound(new { message = "Versión origen no encontrada" })
            : Ok(result);
    }

    [HttpGet("versions/{versionId:guid}/questions")]
    [RequirePermission(PermissionCodes.HealthTestsView)]
    public async Task<ActionResult<IReadOnlyList<HealthTestQuestionDto>>> ListQuestions(
        Guid versionId,
        CancellationToken ct
    ) => Ok(await mediator.Send(new ListQuestionsQuery(versionId), ct));

    // ===================== BATERÍAS =====================

    [HttpGet("batteries")]
    [RequirePermission(PermissionCodes.HealthTestsView)]
    public async Task<ActionResult<PaginatedHealthTestsResult<HealthTestBatteryDto>>> ListBatteries(
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) => Ok(await mediator.Send(new ListBatteriesQuery(search, isActive, page, pageSize), ct));

    [HttpPost("batteries")]
    [RequirePermission(PermissionCodes.HealthTestsManage)]
    public async Task<ActionResult<HealthTestBatteryDto>> CreateBattery(
        [FromBody] CreateBatteryRequest request,
        CancellationToken ct
    ) => Ok(await mediator.Send(new CreateBatteryCommand(request), ct));

    // ===================== ASIGNACIÓN =====================

    [HttpGet("assignments")]
    public async Task<
        ActionResult<PaginatedHealthTestsResult<HealthTestAssignmentDto>>
    > ListAssignments(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? versionId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        // Alcance propio: solo pacientes asignados al profesional del JWT.
        if (ownProfessionalId is { } profId && patientId is null)
        {
            var patientIds = await repository.GetPatientIdsForProfessionalAsync(profId, ct);
            var result = await mediator.Send(
                new ListPendingAssignmentsForProfessionalQuery(patientIds, status, page, pageSize),
                ct
            );
            return Ok(result);
        }

        if (
            ownProfessionalId is { } ownId
            && patientId is { } pId
            && !await repository.PatientBelongsToProfessionalAsync(pId, ownId, ct)
        )
        {
            return NotFound(new { message = "Asignación no encontrada" });
        }

        return Ok(
            await mediator.Send(
                new ListAssignmentsQuery(patientId, versionId, status, page, pageSize),
                ct
            )
        );
    }

    [HttpPost("assignments")]
    public async Task<ActionResult<HealthTestAssignmentDto>> AssignTest(
        [FromBody] AssignTestRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync(PermissionCodes.HealthTestsAssign, ct))
        {
            return Forbid();
        }

        var result = await mediator.Send(
            new AssignTestCommand(
                request.PatientId,
                request.VersionId,
                request.Priority,
                request.DueDate,
                context.UserId
            ),
            ct
        );
        return Ok(result);
    }

    [HttpPost("batteries/{batteryId:guid}/assign")]
    public async Task<ActionResult<IReadOnlyList<HealthTestAssignmentDto>>> AssignBattery(
        Guid batteryId,
        [FromBody] AssignBatteryRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync(PermissionCodes.HealthTestsAssign, ct))
        {
            return Forbid();
        }

        var result = await mediator.Send(
            new AssignBatteryCommand(batteryId, request.PatientIds, context.UserId),
            ct
        );
        return Ok(result);
    }

    [HttpPost("assignments/{id:guid}/cancel")]
    public async Task<ActionResult<HealthTestAssignmentDto>> CancelAssignment(
        Guid id,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync(PermissionCodes.HealthTestsAssign, ct))
        {
            return Forbid();
        }

        var result = await mediator.Send(new CancelAssignmentCommand(id, context.UserId), ct);
        return result is null ? NotFound(new { message = "Asignación no encontrada" }) : Ok(result);
    }

    // ===================== EVALUACIONES / RESULTADOS =====================

    [HttpGet("master")]
    public async Task<ActionResult<IReadOnlyList<MasterPatientRowDto>>> GetMasterRows(
        CancellationToken ct
    )
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        return Ok(await mediator.Send(new GetMasterRowsQuery(ownProfessionalId), ct));
    }

    [HttpGet("patients/{patientId:guid}/evaluations")]
    public async Task<
        ActionResult<PaginatedHealthTestsResult<HealthTestEvaluationDto>>
    > ListPatientEvaluations(
        Guid patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default
    )
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        if (
            ownProfessionalId is { } ownId
            && !await repository.PatientBelongsToProfessionalAsync(patientId, ownId, ct)
        )
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        return Ok(
            await mediator.Send(
                new ListEvaluationsByPatientQuery(
                    patientId,
                    page,
                    pageSize,
                    status,
                    from,
                    to,
                    category
                ),
                ct
            )
        );
    }

    [HttpGet("patients/{patientId:guid}/evaluations/{evaluationId:guid}")]
    public async Task<ActionResult<HealthTestEvaluationDetailDto>> GetPatientEvaluation(
        Guid patientId,
        Guid evaluationId,
        CancellationToken ct
    )
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        if (
            ownProfessionalId is { } ownId
            && !await repository.PatientBelongsToProfessionalAsync(patientId, ownId, ct)
        )
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        var detail = await mediator.Send(new GetEvaluationDetailQuery(patientId, evaluationId), ct);
        return detail is null ? NotFound(new { message = "Evaluación no encontrada" }) : Ok(detail);
    }

    [HttpGet("patients/{patientId:guid}/evaluations/{evaluationId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<HealthTestCommentDto>>> ListEvaluationComments(
        Guid patientId,
        Guid evaluationId,
        CancellationToken ct
    )
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        if (
            ownProfessionalId is { } ownId
            && !await repository.PatientBelongsToProfessionalAsync(patientId, ownId, ct)
        )
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        return Ok(await mediator.Send(new GetEvaluationCommentsQuery(patientId, evaluationId), ct));
    }

    [HttpGet("patients/{patientId:guid}/results")]
    public async Task<ActionResult<IReadOnlyList<HealthTestResultDto>>> ListPatientResults(
        Guid patientId,
        CancellationToken ct
    )
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        if (
            ownProfessionalId is { } ownId
            && !await repository.PatientBelongsToProfessionalAsync(patientId, ownId, ct)
        )
        {
            return NotFound(new { message = "Paciente no encontrado" });
        }

        return Ok(await mediator.Send(new ListResultsByPatientQuery(patientId), ct));
    }

    // ===================== INDICADORES =====================

    [HttpGet("indicators")]
    [RequirePermission(PermissionCodes.HealthTestsView)]
    public async Task<ActionResult<IReadOnlyList<HealthTestIndicatorDef>>> ListIndicators(
        CancellationToken ct
    ) => Ok(await repository.ListActiveIndicatorDefsAsync(ct));

    // ===================== ALERTAS =====================

    [HttpGet("alerts")]
    public async Task<ActionResult<PaginatedHealthTestsResult<HealthTestAlertDto>>> ListAlerts(
        [FromQuery] Guid? patientId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? severity = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        return Ok(
            await mediator.Send(
                new ListAlertsQuery(patientId, status, severity, page, pageSize),
                ct
            )
        );
    }

    [HttpPost("alerts/{id:guid}/review")]
    [RequirePermission(PermissionCodes.HealthTestsReview)]
    public async Task<ActionResult<HealthTestAlertDto>> ReviewAlert(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(
            new TransitionAlertCommand(id, HealthTestAlertStatus.reviewing, context.UserId),
            ct
        );
        return result is null ? NotFound(new { message = "Alerta no encontrada" }) : Ok(result);
    }

    [HttpPost("alerts/{id:guid}/resolve")]
    [RequirePermission(PermissionCodes.HealthTestsReview)]
    public async Task<ActionResult<HealthTestAlertDto>> ResolveAlert(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(
            new TransitionAlertCommand(id, HealthTestAlertStatus.resolved, context.UserId),
            ct
        );
        return result is null ? NotFound(new { message = "Alerta no encontrada" }) : Ok(result);
    }

    [HttpPost("alerts/{id:guid}/close")]
    [RequirePermission(PermissionCodes.HealthTestsReview)]
    public async Task<ActionResult<HealthTestAlertDto>> CloseAlert(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(
            new TransitionAlertCommand(id, HealthTestAlertStatus.closed, context.UserId),
            ct
        );
        return result is null ? NotFound(new { message = "Alerta no encontrada" }) : Ok(result);
    }

    [HttpPost("comments")]
    [RequirePermission(PermissionCodes.HealthTestsReview)]
    public async Task<ActionResult<Guid>> AddComment(
        [FromBody] AddCommentRequest request,
        CancellationToken ct
    ) => Ok(await mediator.Send(new AddCommentCommand(request, context.UserId ?? Guid.Empty), ct));

    // ===================== STATS (dashboard ERP) =====================

    [HttpGet("stats")]
    public async Task<ActionResult<HealthTestStatsDto>> GetStats(CancellationToken ct)
    {
        var (allowed, ownProfessionalId) = await ResolveScopeAsync(ct);
        if (!allowed)
        {
            return Forbid();
        }

        return Ok(await mediator.Send(new GetHealthTestStatsQuery(ownProfessionalId), ct));
    }

    // ===================== HELPERS =====================

    /// <summary>
    /// Resuelve el alcance de datos: HealthTests.View (global) o
    /// HealthTests.ViewOwn (solo pacientes asignados al profesional del JWT).
    /// La autorización es positiva: sin ninguno de los dos, no hay acceso.
    /// </summary>
    private async Task<(bool Allowed, Guid? OwnProfessionalId)> ResolveScopeAsync(
        CancellationToken ct
    )
    {
        if (await context.HasPermissionAsync(PermissionCodes.HealthTestsView, ct))
        {
            return (true, null);
        }

        if (await context.HasPermissionAsync(PermissionCodes.HealthTestsViewOwn, ct))
        {
            return (true, await context.GetProfessionalIdAsync(ct));
        }

        return (false, null);
    }
}

/// <summary>Request de asignación individual de un test (scope resuelto por JWT).</summary>
public record AssignTestRequest(Guid PatientId, Guid VersionId, int? Priority, DateTime? DueDate);

/// <summary>Request de asignación masiva de una batería.</summary>
public record AssignBatteryRequest(IReadOnlyList<Guid> PatientIds);
