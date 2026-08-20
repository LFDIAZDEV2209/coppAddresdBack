using CoppAddresd.Application.Features.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/inventory/movements")]
[Authorize]
public class InventoryMovementsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedMovementsResult>> List(
        [FromQuery] string? search = null,
        [FromQuery] string? direction = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListMovementsQuery(search, direction, from, to, page, pageSize), ct));
}
