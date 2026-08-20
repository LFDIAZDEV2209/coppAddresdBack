using CoppAddresd.Application.Features.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/inventory/exits")]
[Authorize]
public class InventoryExitsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedExitsResult>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListExitsQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryExitDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetExitQuery(id), ct);
        return result is null ? NotFound(new { message = "Salida no encontrada" }) : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<InventoryExitDto>> Create(
        [FromBody] CreateExitRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateExitCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
