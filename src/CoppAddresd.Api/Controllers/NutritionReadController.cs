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
    [ProducesResponseType(typeof(NutritionInboxPage), StatusCodes.Status200OK)]
    public async Task<ActionResult<NutritionInboxPage>> Inbox([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? state = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new NutritionInboxQuery(await ActorAsync(ct), page, pageSize, state, search), ct));

    /// <summary>Alimentos del catálogo nutricional común para el selector del editor.</summary>
    [HttpGet("foods")]
    [ProducesResponseType(typeof(IReadOnlyList<NutritionFoodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NutritionFoodDto>>> Foods(CancellationToken ct)
        => Ok(await mediator.Send(new NutritionFoodCatalogQuery(await ActorAsync(ct)), ct));

    private async Task<NutritionActor> ActorAsync(CancellationToken ct) => new(
        context.UserId ?? throw new UnauthorizedAccessException(), await context.GetProfessionalIdAsync(ct),
        User.HasClaim(PermissionClaimTypes.Permission, PermissionCodes.PatientsView),
        await context.HasPermissionAsync(PermissionCodes.NutritionManage, ct), await context.HasPermissionAsync(PermissionCodes.NutritionApprove, ct));
}
