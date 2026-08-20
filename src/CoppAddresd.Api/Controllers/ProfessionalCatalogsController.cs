using CoppAddresd.Application.Features.Professionals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>Catálogos profesionales del ERP (profesiones y especialidades).</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ProfessionalCatalogsController(IMediator mediator) : ControllerBase
{
    /// <summary>Catálogo de profesiones con sus especialidades válidas.</summary>
    [HttpGet("professional-types")]
    public async Task<ActionResult<IReadOnlyList<ProfessionalTypeDto>>> ProfessionalTypes(CancellationToken ct)
    {
        var types = await mediator.Send(new ListProfessionalTypesQuery(), ct);
        return Ok(types);
    }

    /// <summary>Catálogo de especialidades agrupado por categoría.</summary>
    [HttpGet("specialties")]
    public async Task<ActionResult<IReadOnlyList<SpecialtyDto>>> Specialties(CancellationToken ct)
    {
        var specialties = await mediator.Send(new ListSpecialtiesQuery(), ct);
        return Ok(specialties);
    }
}
