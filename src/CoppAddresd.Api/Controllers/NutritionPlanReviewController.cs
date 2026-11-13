using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class NutritionPlanReviewController(INutritionPlanWorkflow workflow, ICurrentContext context,
    IPatientScopeGuard? patientScope = null) : ControllerBase
{
    [HttpGet("me/nutrition-plan/status")]
    public async Task<ActionResult<NutritionPlanStatusDto>> Status([FromQuery] string? timezone, CancellationToken ct)
    {
        if (context.UserId is not { } userId) return Unauthorized();
        return Ok(await workflow.GetStatusAsync(userId, timezone, ct));
    }

    [HttpPost("wellness/nutrition-plans/{id:guid}/approve")]
    [RequireContextPermission(PermissionCodes.NutritionPlansApprove)]
    [PatientScope(PatientResource.NutritionPlan, "id", "Nutrition.Approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveNutritionPlanRequest request, CancellationToken ct)
        => Ok((await workflow.ApproveAsync([new(id, request.Revision, request.Notes)], await ActorAsync(ct), ct))[0]);

    [HttpPost("wellness/nutrition-plans/approve")]
    [RequireContextPermission(PermissionCodes.NutritionPlansApprove)]
    public async Task<IActionResult> ApproveBatch([FromBody] ApproveNutritionPlansRequest request, CancellationToken ct)
    {
        var guard = patientScope ?? HttpContext.RequestServices.GetRequiredService<IPatientScopeGuard>();
        var allowed = new List<NutritionPlanApprovalInput>();
        var omitted = new List<Guid>();
        foreach (var plan in request.Plans)
        {
            try
            {
                await guard.EnsureResourceAsync(PatientResource.NutritionPlan, plan.Id, "Nutrition.Approve", ct);
                allowed.Add(plan);
            }
            catch (CoppAddresd.Domain.Exceptions.NotFoundException) { omitted.Add(plan.Id); }
        }
        // Se preserva el cuerpo de respuesta existente; solo se reportan ids que envió el actor.
        Response.Headers["X-Omitted-Without-Scope-Count"] = omitted.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Omitted-Without-Scope"] = string.Join(",", omitted);
        return Ok(allowed.Count == 0 ? [] : await workflow.ApproveAsync(allowed, await ActorAsync(ct), ct));
    }

    [HttpGet("wellness/nutrition-plans/{id:guid}/history")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    [PatientScope(PatientResource.NutritionPlan, "id", "Nutrition.View")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        var rows = await workflow.HistoryAsync(id, await ActorAsync(ct), ct);
        return Ok(await context.HasPermissionAsync(PermissionCodes.NutritionNotesView, ct)
            ? rows : rows.Select(e => e with { Notes = null }).ToArray());
    }

    private async Task<NutritionReviewActor> ActorAsync(CancellationToken ct) => new(
        context.UserId ?? throw new UnauthorizedAccessException(), await context.GetProfessionalIdAsync(ct),
        // Solo Admin exceptúa la asignación por paciente.
        User.IsInRole("Admin"));
}

public sealed record ApproveNutritionPlanRequest(int Revision, string? Notes = null);
public sealed record ApproveNutritionPlansRequest(IReadOnlyList<NutritionPlanApprovalInput> Plans);
