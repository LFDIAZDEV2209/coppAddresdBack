using CoppAddresd.Application.Features.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/inventory/analytics")]
[Authorize]
public class InventoryAnalyticsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InventoryAnalyticsDto>> Get(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new GetInventoryAnalyticsQuery(from, to), ct));
}
