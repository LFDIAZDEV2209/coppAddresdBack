using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/wellness")]
[Authorize]
public class WellnessController(
    IMediator mediator,
    ILogger<WellnessController> logger,
    ICurrentContext context,
    INutritionPlanWorkflow nutritionWorkflow) : ControllerBase
{
    // ===================== NUTRITION PLANS =====================

    [HttpGet("nutrition-plans")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    public async Task<ActionResult<PaginatedNutritionPlanResult>> ListNutritionPlans(
        [FromQuery] bool? isTemplate = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? patientId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var actor = await NutritionActorAsync(ct);
        if (!actor.ViewAll && actor.ProfessionalId is null) return NotFound();
        if (patientId is { } patient && !await nutritionWorkflow.CanAccessPatientAsync(patient, actor, ct)) return NotFound();
        return Ok(await mediator.Send(new ListNutritionPlansQuery(isTemplate, search, status, patientId, page, pageSize,
            actor.ViewAll ? null : actor.ProfessionalId), ct));
    }

    [HttpGet("nutrition-plans/{id:guid}")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    [PatientScope(PatientResource.NutritionPlan, "id", "Nutrition.View", AllowUnowned = true)]
    public async Task<ActionResult<NutritionPlanDto>> GetNutritionPlan(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNutritionPlanQuery(id), ct);
        if (result?.PatientId is { } patient && !await nutritionWorkflow.CanAccessPatientAsync(patient, await NutritionActorAsync(ct), ct)) return NotFound();
        return result is null ? NotFound(new { message = "Plan de alimentación no encontrado" }) : Ok(result);
    }

    [HttpPost("nutrition-plans")]
    [RequireContextPermission(PermissionCodes.NutritionManage)]
    [PatientScope("request.PatientId", "Nutrition.Manage", Optional = true)]
    [PatientScope(PatientResource.NutritionPlan, "request.SourcePlanId", "Nutrition.Manage", Optional = true, AllowUnowned = true)]
    public async Task<ActionResult<NutritionPlanDto>> CreateNutritionPlan(
        [FromBody] CreateNutritionPlanRequest request, CancellationToken ct)
    {
        if (!request.IsTemplate && (request.PatientId is not { } patient
            || !await nutritionWorkflow.CanAccessPatientAsync(patient, await NutritionActorAsync(ct), ct))) return NotFound();
        var result = await mediator.Send(new CreateNutritionPlanCommand(request, context.UserId), ct);
        return CreatedAtAction(nameof(GetNutritionPlan), new { id = result.Id }, result);
    }

    [HttpPut("nutrition-plans/{id:guid}")]
    [RequireContextPermission(PermissionCodes.NutritionManage)]
    [PatientScope(PatientResource.NutritionPlan, "id", "Nutrition.Manage", AllowUnowned = true)]
    public async Task<ActionResult<NutritionPlanDto>> UpdateNutritionPlan(
        Guid id, [FromBody] UpdateNutritionPlanRequest request, CancellationToken ct)
    {
        var existing = await mediator.Send(new GetNutritionPlanQuery(id), ct);
        if (existing is null || existing.PatientId is { } patient
            && !await nutritionWorkflow.CanAccessPatientAsync(patient, await NutritionActorAsync(ct), ct)) return NotFound();
        var result = await mediator.Send(new UpdateNutritionPlanCommand(id, request, context.UserId), ct);
        return result is null ? NotFound(new { message = "Plan de alimentación no encontrado" }) : Ok(result);
    }

    [HttpDelete("nutrition-plans/{id:guid}")]
    [RequireContextPermission(PermissionCodes.NutritionManage)]
    [PatientScope(PatientResource.NutritionPlan, "id", "Nutrition.Manage", AllowUnowned = true)]
    public async Task<IActionResult> DeleteNutritionPlan(Guid id, CancellationToken ct)
    {
        var existing = await mediator.Send(new GetNutritionPlanQuery(id), ct);
        if (existing is null || existing.PatientId is { } patient
            && !await nutritionWorkflow.CanAccessPatientAsync(patient, await NutritionActorAsync(ct), ct)) return NotFound();
        var deleted = await mediator.Send(new DeleteNutritionPlanCommand(id, context.UserId), ct);
        return deleted ? NoContent() : NotFound(new { message = "Plan de alimentación no encontrado" });
    }

    [HttpPost("nutrition-plans/{sourcePlanId:guid}/clone")]
    [RequireContextPermission(PermissionCodes.NutritionManage)]
    [PatientScope(PatientResource.NutritionPlan, "sourcePlanId", "Nutrition.Manage", AllowUnowned = true)]
    [PatientScope("patientId", "Nutrition.Manage", Optional = true)]
    public async Task<ActionResult<NutritionPlanDto>> CloneNutritionPlan(
        Guid sourcePlanId, [FromQuery] Guid? patientId, CancellationToken ct)
    {
        var source = await mediator.Send(new GetNutritionPlanQuery(sourcePlanId), ct);
        var actor = await NutritionActorAsync(ct);
        if (source is null || source.PatientId is { } sourcePatient && !await nutritionWorkflow.CanAccessPatientAsync(sourcePatient, actor, ct)
            || patientId is { } target && !await nutritionWorkflow.CanAccessPatientAsync(target, actor, ct)) return NotFound();
        var result = await mediator.Send(new CloneNutritionPlanCommand(sourcePlanId, patientId, context.UserId), ct);
        return result is null
            ? NotFound(new { message = "Plan fuente no encontrado" })
            : CreatedAtAction(nameof(GetNutritionPlan), new { id = result.Id }, result);
    }

    // ===================== EXERCISE ROUTINES =====================

    [HttpGet("exercise-routines")]
    [RequireContextPermission(PermissionCodes.WellnessView)]
    public async Task<ActionResult<PaginatedExerciseRoutineResult>> ListExerciseRoutines(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListExerciseRoutinesQuery(search, status, category, page, pageSize), ct));

    [HttpGet("exercise-routines/{id:guid}")]
    [RequireContextPermission(PermissionCodes.WellnessView)]
    public async Task<ActionResult<ExerciseRoutineDto>> GetExerciseRoutine(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetExerciseRoutineQuery(id), ct);
        return result is null ? NotFound(new { message = "Rutina de ejercicio no encontrada" }) : Ok(result);
    }

    [HttpPost("exercise-routines")]
    [RequireContextPermission(PermissionCodes.WellnessManage)]
    [PatientScope("request.PatientId", "Wellness.Manage", Optional = true)]
    public async Task<ActionResult<ExerciseRoutineDto>> CreateExerciseRoutine(
        [FromBody] CreateExerciseRoutineRequest request, CancellationToken ct)
    {
        if (request.PatientId is { } patient && !await nutritionWorkflow.CanAccessPatientAsync(patient, await NutritionActorAsync(ct), ct)) return NotFound();
        var result = await mediator.Send(new CreateExerciseRoutineCommand(request, context.UserId), ct);
        return CreatedAtAction(nameof(GetExerciseRoutine), new { id = result.Id }, result);
    }

    [HttpPut("exercise-routines/{id:guid}")]
    [RequireContextPermission(PermissionCodes.WellnessManage)]
    public async Task<ActionResult<ExerciseRoutineDto>> UpdateExerciseRoutine(
        Guid id, [FromBody] UpdateExerciseRoutineRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateExerciseRoutineCommand(id, request), ct);
        return result is null ? NotFound(new { message = "Rutina de ejercicio no encontrada" }) : Ok(result);
    }

    [HttpDelete("exercise-routines/{id:guid}")]
    [RequireContextPermission(PermissionCodes.WellnessManage)]
    public async Task<IActionResult> DeleteExerciseRoutine(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.Send(new DeleteExerciseRoutineCommand(id), ct);
        return deleted ? NoContent() : NotFound(new { message = "Rutina de ejercicio no encontrada" });
    }

    // ===================== ROUTINE ASSIGNMENTS =====================

    [HttpGet("routine-assignments")]
    [RequireContextPermission(PermissionCodes.WellnessView)]
    public async Task<ActionResult<PaginatedRoutineAssignmentResult>> ListRoutineAssignments(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? routineId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var actor = await NutritionActorAsync(ct);
        if (!actor.ViewAll && actor.ProfessionalId is null) return NotFound();
        if (patientId is { } patient && !await nutritionWorkflow.CanAccessPatientAsync(patient, actor, ct)) return NotFound();
        return Ok(await mediator.Send(new ListRoutineAssignmentsQuery(patientId, routineId, status, page, pageSize,
            actor.ViewAll ? null : actor.ProfessionalId), ct));
    }

    [HttpGet("routine-assignments/{id:guid}")]
    [RequireContextPermission(PermissionCodes.WellnessView)]
    [PatientScope(PatientResource.RoutineAssignment, "id", "Wellness.View")]
    public async Task<ActionResult<RoutineAssignmentDto>> GetRoutineAssignment(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRoutineAssignmentQuery(id), ct);
        if (result is not null && !await nutritionWorkflow.CanAccessPatientAsync(result.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        return result is null ? NotFound(new { message = "Asignación no encontrada" }) : Ok(result);
    }

    [HttpGet("patients/{patientId:guid}/routine-assignments")]
    [RequireContextPermission(PermissionCodes.WellnessView)]
    [PatientScope("patientId", "Wellness.View")]
    public async Task<ActionResult<IReadOnlyList<RoutineAssignmentDto>>> ListAssignmentsByPatient(
        Guid patientId, CancellationToken ct)
    {
        if (!await nutritionWorkflow.CanAccessPatientAsync(patientId, await NutritionActorAsync(ct), ct)) return NotFound();
        return Ok(await mediator.Send(new ListAssignmentsByPatientQuery(patientId), ct));
    }

    [HttpPost("routine-assignments")]
    [RequireContextPermission(PermissionCodes.WellnessManage)]
    [PatientScope("request.PatientId", "Wellness.Manage")]
    public async Task<ActionResult<RoutineAssignmentDto>> CreateRoutineAssignment(
        [FromBody] CreateRoutineAssignmentRequest request, CancellationToken ct)
    {
        if (!await nutritionWorkflow.CanAccessPatientAsync(request.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        var result = await mediator.Send(new CreateRoutineAssignmentCommand(request, context.UserId), ct);
        return CreatedAtAction(nameof(GetRoutineAssignment), new { id = result.Id }, result);
    }

    [HttpPut("routine-assignments/{id:guid}")]
    [RequireContextPermission(PermissionCodes.WellnessManage)]
    [PatientScope(PatientResource.RoutineAssignment, "id", "Wellness.Manage")]
    public async Task<ActionResult<RoutineAssignmentDto>> UpdateRoutineAssignment(
        Guid id, [FromBody] UpdateRoutineAssignmentRequest request, CancellationToken ct)
    {
        var assignment = await mediator.Send(new GetRoutineAssignmentQuery(id), ct);
        if (assignment is null || !await nutritionWorkflow.CanAccessPatientAsync(assignment.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        var result = await mediator.Send(new UpdateRoutineAssignmentCommand(id, request), ct);
        return result is null ? NotFound(new { message = "Asignación no encontrada" }) : Ok(result);
    }

    [HttpDelete("routine-assignments/{id:guid}")]
    [RequireContextPermission(PermissionCodes.WellnessManage)]
    [PatientScope(PatientResource.RoutineAssignment, "id", "Wellness.Manage")]
    public async Task<IActionResult> DeleteRoutineAssignment(Guid id, CancellationToken ct)
    {
        var assignment = await mediator.Send(new GetRoutineAssignmentQuery(id), ct);
        if (assignment is null || !await nutritionWorkflow.CanAccessPatientAsync(assignment.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        var deleted = await mediator.Send(new DeleteRoutineAssignmentCommand(id), ct);
        return deleted ? NoContent() : NotFound(new { message = "Asignación no encontrada" });
    }

    // ===================== NUTRITION PLAN ASSIGNMENTS =====================

    [HttpGet("nutrition-plan-assignments")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    public async Task<ActionResult<PaginatedNutritionPlanAssignmentResult>> ListNutritionPlanAssignments(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? planId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var actor = await NutritionActorAsync(ct);
        if (!actor.ViewAll && actor.ProfessionalId is null) return NotFound();
        if (patientId is { } patient && !await nutritionWorkflow.CanAccessPatientAsync(patient, actor, ct)) return NotFound();
        if (planId is { } plan)
        {
            var source = await mediator.Send(new GetNutritionPlanQuery(plan), ct);
            if (source is null || source.PatientId is { } owner
                && !await nutritionWorkflow.CanAccessPatientAsync(owner, actor, ct)) return NotFound();
        }
        return Ok(await mediator.Send(new ListNutritionPlanAssignmentsQuery(patientId, planId, status, page, pageSize,
            actor.ViewAll ? null : actor.ProfessionalId), ct));
    }

    [HttpGet("nutrition-plan-assignments/{id:guid}")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    [PatientScope(PatientResource.NutritionAssignment, "id", "Nutrition.View")]
    public async Task<ActionResult<NutritionPlanAssignmentDto>> GetNutritionPlanAssignment(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNutritionPlanAssignmentQuery(id), ct);
        if (result is not null && !await nutritionWorkflow.CanAccessPatientAsync(result.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        return result is null ? NotFound(new { message = "Asignación no encontrada" }) : Ok(result);
    }

    [HttpGet("patients/{patientId:guid}/nutrition-plan-assignments")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    [PatientScope("patientId", "Nutrition.View")]
    public async Task<ActionResult<IReadOnlyList<NutritionPlanAssignmentDto>>> ListPlanAssignmentsByPatient(
        Guid patientId, CancellationToken ct)
    {
        if (!await nutritionWorkflow.CanAccessPatientAsync(patientId, await NutritionActorAsync(ct), ct)) return NotFound();
        return Ok(await mediator.Send(new ListPlanAssignmentsByPatientQuery(patientId), ct));
    }

    [HttpPost("nutrition-plan-assignments")]
    [RequireContextPermission(PermissionCodes.NutritionManage)]
    [PatientScope("request.PatientId", "Nutrition.Manage")]
    [PatientScope(PatientResource.NutritionPlan, "request.PlanId", "Nutrition.Manage", AllowUnowned = true)]
    public async Task<ActionResult<NutritionPlanAssignmentDto>> CreateNutritionPlanAssignment(
        [FromBody] CreateNutritionPlanAssignmentRequest request, CancellationToken ct)
    {
        if (!await nutritionWorkflow.CanAccessPatientAsync(request.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        var result = await mediator.Send(new CreateNutritionPlanAssignmentCommand(request, context.UserId), ct);
        return CreatedAtAction(nameof(GetNutritionPlanAssignment), new { id = result.Id }, result);
    }

    [HttpPut("nutrition-plan-assignments/{id:guid}")]
    [RequireContextPermission(PermissionCodes.NutritionManage)]
    [PatientScope(PatientResource.NutritionAssignment, "id", "Nutrition.Manage")]
    public async Task<ActionResult<NutritionPlanAssignmentDto>> UpdateNutritionPlanAssignment(
        Guid id, [FromBody] UpdateNutritionPlanAssignmentRequest request, CancellationToken ct)
    {
        var assignment = await mediator.Send(new GetNutritionPlanAssignmentQuery(id), ct);
        if (assignment is null || !await nutritionWorkflow.CanAccessPatientAsync(assignment.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        var result = await mediator.Send(new UpdateNutritionPlanAssignmentCommand(id, request), ct);
        return result is null ? NotFound(new { message = "Asignación no encontrada" }) : Ok(result);
    }

    [HttpDelete("nutrition-plan-assignments/{id:guid}")]
    [RequireContextPermission(PermissionCodes.NutritionManage)]
    [PatientScope(PatientResource.NutritionAssignment, "id", "Nutrition.Manage")]
    public async Task<IActionResult> DeleteNutritionPlanAssignment(Guid id, CancellationToken ct)
    {
        var assignment = await mediator.Send(new GetNutritionPlanAssignmentQuery(id), ct);
        if (assignment is null || !await nutritionWorkflow.CanAccessPatientAsync(assignment.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        var deleted = await mediator.Send(new DeleteNutritionPlanAssignmentCommand(id), ct);
        return deleted ? NoContent() : NotFound(new { message = "Asignación no encontrada" });
    }

    // ===================== AI PLAN GENERATION =====================

    /// <summary>
    /// Genera un plan de alimentación o ejercicio con IA, condicionado por el
    /// contexto clínico consolidado del paciente y las reglas de seguridad
    /// activas. El plan devuelto llega listo para el formulario del frontend.
    /// </summary>
    [HttpPost("plans/generate")]
    [PatientScope("request.PatientId", "Wellness.Manage", NutritionGeneration = true)]
    public async Task<ActionResult<GeneratePlanResponse>> GeneratePlan(
        [FromBody] GeneratePlanRequest request,
        CancellationToken ct)
    {
        if (request.Type is not ("nutrition" or "exercise"))
            return BadRequest(new { error = "Tipo de plan inválido. Valores permitidos: nutrition, exercise." });

        if (!await nutritionWorkflow.CanAccessPatientAsync(request.PatientId, await NutritionActorAsync(ct), ct)) return NotFound();
        try
        {
            var result = await mediator.Send(
                new GeneratePlanCommand(request.PatientId, request.Type), ct);
            return Ok(result);
        }
        catch (AiServiceException ex)
        {
            logger.LogError(ex,
                "AI Service rechazó la generación del plan (status {Status}): {Detail}",
                ex.StatusCode, ex.Detail);
            return StatusCode(502, new { error = "No fue posible generar el plan." });
        }
    }

    private async Task<NutritionReviewActor> NutritionActorAsync(CancellationToken ct) => new(
        context.UserId ?? throw new UnauthorizedAccessException(), await context.GetProfessionalIdAsync(ct),
        User.IsInRole("Admin"));
}
