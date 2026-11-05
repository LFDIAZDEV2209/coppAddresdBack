using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/nutrition/patients/{patientId:guid}/prescriptions")]
public sealed class NutritionController(IMediator mediator, ICurrentContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await mediator.Send(new ListNutritionCasesQuery(patientId, await ActorAsync(ct), page, pageSize), ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid patientId, Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetNutritionCaseQuery(patientId, id, await ActorAsync(ct)), ct));
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid patientId, Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new NutritionHistoryQuery(patientId, id, await ActorAsync(ct)), ct));
    [HttpPost]
    public async Task<IActionResult> Generate(Guid patientId, [FromBody] NutritionGenerateInput input, CancellationToken ct)
    {
        var plan = await mediator.Send(new NutritionCaseCommand(NutritionOperation.Generate, patientId, null, null,
            await ActorAsync(ct), Generate: input), ct);
        return CreatedAtAction(nameof(Get), new { patientId, id = plan.Id }, plan);
    }
    [HttpPost("{id:guid}/{action}")]
    public async Task<IActionResult> Act(Guid patientId, Guid id, string action, [FromBody] NutritionActionRequest input, CancellationToken ct)
    {
        var operation = action switch { "save" => NutritionOperation.Save, "apply-day" => NutritionOperation.ApplyDay,
            "create-version" => NutritionOperation.CreateVersion, "submit" => NutritionOperation.Submit,
            "approve" => NutritionOperation.Approve, "return" => NutritionOperation.Return,
            "regenerate" => NutritionOperation.Regenerate, _ => throw new NotFoundException("Acción no encontrada.") };
        return Ok(await mediator.Send(new NutritionCaseCommand(operation, patientId, id, input.Revision,
            await ActorAsync(ct), input.Generate, input.Edit, input.Comment, input.CriticalConfirmed, input.StartDate), ct));
    }
    private async Task<NutritionActor> ActorAsync(CancellationToken ct) => new(
        context.UserId ?? throw new UnauthorizedAccessException(), await context.GetProfessionalIdAsync(ct),
        User.HasClaim(PermissionClaimTypes.Permission, PermissionCodes.PatientsView),
        await context.HasPermissionAsync("Nutrition.Manage", ct), await context.HasPermissionAsync("Nutrition.Approve", ct));
}
public sealed record NutritionActionRequest(int Revision, NutritionEdit? Edit = null,
    NutritionGenerateInput? Generate = null, string? Comment = null, bool CriticalConfirmed = false, DateOnly? StartDate = null);
