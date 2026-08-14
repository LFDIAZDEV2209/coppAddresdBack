using CoppAddresd.Application.Features.Patients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class InsurersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InsurerDto>>> List(CancellationToken ct)
    {
        var insurers = await mediator.Send(new ListInsurersQuery(), ct);
        return Ok(insurers);
    }
}