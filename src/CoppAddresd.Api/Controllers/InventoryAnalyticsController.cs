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
    public async Task<ActionResult<InventoryAnalyticsDto>> Get(CancellationToken ct)
        => Ok(await mediator.Send(new GetInventoryAnalyticsQuery(), ct));
}
