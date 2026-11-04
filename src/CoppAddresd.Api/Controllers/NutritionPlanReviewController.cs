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
public sealed class NutritionPlanReviewController(INutritionPlanWorkflow workflow, ICurrentContext context) : ControllerBase
{
    [HttpGet("me/nutrition-plan/status")]
    public async Task<ActionResult<NutritionPlanStatusDto>> Status([FromQuery] string? timezone, CancellationToken ct)
    {
        if (context.UserId is not { } userId) return Unauthorized();
        return Ok(await workflow.GetStatusAsync(userId, timezone, ct));
    }

    [HttpPost("wellness/nutrition-plans/{id:guid}/approve")]
    [RequirePermission(PermissionCodes.NutritionPlansApprove)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveNutritionPlanRequest request, CancellationToken ct)
        => Ok((await workflow.ApproveAsync([new(id, request.Revision, request.Notes)], await ActorAsync(ct), ct))[0]);

    [HttpPost("wellness/nutrition-plans/approve")]
    [RequirePermission(PermissionCodes.NutritionPlansApprove)]
    public async Task<IActionResult> ApproveBatch([FromBody] ApproveNutritionPlansRequest request, CancellationToken ct)
        => Ok(await workflow.ApproveAsync(request.Plans, await ActorAsync(ct), ct));

    [HttpGet("wellness/nutrition-plans/{id:guid}/history")]
    [RequirePermission(PermissionCodes.WellnessView)]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
        => Ok(await workflow.HistoryAsync(id, await ActorAsync(ct), ct));

    private async Task<NutritionReviewActor> ActorAsync(CancellationToken ct) => new(
        context.UserId ?? throw new UnauthorizedAccessException(), await context.GetProfessionalIdAsync(ct),
        // Solo el permiso GLOBAL del JWT habilita toda la población. Un permiso
        // scoped no convierte el header de clínica en acceso global.
        User.HasClaim(PermissionClaimTypes.Permission, PermissionCodes.PatientsView));
}

public sealed record ApproveNutritionPlanRequest(int Revision, string? Notes = null);
public sealed record ApproveNutritionPlansRequest(IReadOnlyList<NutritionPlanApprovalInput> Plans);
