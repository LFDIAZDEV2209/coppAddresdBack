using CoppAddresd.Application.Interfaces;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Authorize(Policy = NutritionPolicies.Staff)]
[Route("api/v1/nutrition")]
public sealed class NutritionReadController(IMediator mediator, ICurrentContext context) : ControllerBase
{
    /// <summary>Bandeja del alcance efectivo del actor; incluye pacientes sin prescripción.</summary>
    [HttpGet("inbox")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    [ProducesResponseType(typeof(NutritionInboxPage), StatusCodes.Status200OK)]
    public async Task<ActionResult<NutritionInboxPage>> Inbox([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? state = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new NutritionInboxQuery(await ActorAsync(ct), page, pageSize, state, search), ct));

    /// <summary>Alimentos del catálogo nutricional común para el selector del editor.</summary>
    [HttpGet("foods")]
    [RequireContextPermission(PermissionCodes.NutritionView)]
    [ProducesResponseType(typeof(IReadOnlyList<NutritionFoodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NutritionFoodDto>>> Foods(CancellationToken ct)
        => Ok(await mediator.Send(new NutritionFoodCatalogQuery(await ActorAsync(ct)), ct));

    /// <summary>Reserva o recupera la generación; el worker construye el borrador fuera de la petición.</summary>
    [HttpPost("patients/{patientId:guid}/draft-generation")]
    [ProducesResponseType(typeof(NutritionDraftGenerationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(NutritionDraftGenerationResult), StatusCodes.Status200OK)]
    [PatientScope("patientId", "Nutrition.Manage")]
    public async Task<IActionResult> DraftGeneration(Guid patientId, CancellationToken ct)
    {
        var result = await mediator.Send(new NutritionDraftGenerationCommand(patientId, await ActorAsync(ct)), ct);
        return result.State == "ready" ? Ok(result) : StatusCode(StatusCodes.Status202Accepted, result);
    }

    private async Task<NutritionActor> ActorAsync(CancellationToken ct) => new(
        context.UserId ?? throw new UnauthorizedAccessException(), await context.GetProfessionalIdAsync(ct),
        User.IsInRole("Admin"),
        await context.HasPermissionAsync(PermissionCodes.NutritionManage, ct), await context.HasPermissionAsync(PermissionCodes.NutritionApprove, ct),
        await context.HasPermissionAsync(PermissionCodes.NutritionView, ct),
        await context.HasPermissionAsync(PermissionCodes.NutritionNotesView, ct),
        await context.HasPermissionAsync(PermissionCodes.NutritionPhotosView, ct));

    /// <summary>Ficha completa bajo alcance Nutrition; no exige permisos ajenos para identidad o perfil.</summary>
    [HttpGet("patients/{patientId:guid}/workspace")]
    [PatientScope("patientId", "Nutrition.View")]
    public async Task<ActionResult<NutritionWorkspaceView>> Workspace(Guid patientId, [FromQuery] int historyPage = 1,
        [FromQuery] int historyPageSize = 20, [FromQuery] Guid? prescriptionId = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new NutritionWorkspaceQuery(patientId, await ActorAsync(ct), historyPage, historyPageSize, prescriptionId), ct));
}
