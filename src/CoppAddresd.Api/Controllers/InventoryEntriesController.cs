using CoppAddresd.Application.Features.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/inventory/entries")]
[Authorize]
public class InventoryEntriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedEntriesResult>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListEntriesQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryEntryDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetEntryQuery(id), ct);
        return result is null ? NotFound(new { message = "Entrada no encontrada" }) : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<InventoryEntryDto>> Create(
        [FromBody] CreateEntryRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateEntryCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
