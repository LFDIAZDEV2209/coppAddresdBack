using CoppAddresd.Application.Features.Store;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/store/[controller]")]
[Authorize]
public class ItemsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedStoreItemsResult>> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListStoreItemsQuery(status, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoreItemDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetStoreItemQuery(id), ct);
        return result is null ? NotFound(new { message = "Ítem de tienda no encontrado" }) : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<StoreItemDto>> Create(
        [FromBody] CreateStoreItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateStoreItemCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StoreItemDto>> Update(
        Guid id, [FromBody] UpdateStoreItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateStoreItemCommand(id, request), ct);
        return result is null ? NotFound(new { message = "Ítem de tienda no encontrado" }) : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Hide(Guid id, CancellationToken ct)
    {
        var hidden = await mediator.Send(new HideStoreItemCommand(id), ct);
        return hidden ? NoContent() : NotFound(new { message = "Ítem de tienda no encontrado" });
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var restored = await mediator.Send(new RestoreStoreItemCommand(id), ct);
        return restored ? Ok(new { message = "Ítem restaurado" }) : NotFound(new { message = "Ítem de tienda no encontrado" });
    }

    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> DeletePermanent(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.Send(new DeleteStoreItemCommand(id), ct);
        return deleted ? NoContent() : NotFound(new { message = "Ítem de tienda no encontrado" });
    }
}
