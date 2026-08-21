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

    /// <summary>
    /// Catálogo de profesionales clínicos con sus especialidades y sedes.
    /// Accesible para cualquier usuario autenticado (como el resto de catálogos):
    /// lo consume la UI de Telemedicina para elegir profesional y el directorio
    /// admin. Solo datos de identidad/contexto, sin PHI.
    /// </summary>
    [HttpGet("professionals-catalog")]
    public async Task<ActionResult<PaginatedProfessionalsCatalogResult>> ProfessionalsCatalog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? specialtyId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] Guid? clinicId = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new ListProfessionalsCatalogQuery(
                page, pageSize, search, status, specialtyId, locationId, organizationId, clinicId),
            ct);
        return Ok(result);
    }
}
