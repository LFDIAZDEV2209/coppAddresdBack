using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Catálogos profesionales del ERP (profesiones y especialidades). Las
/// mutaciones exigen Professionals.Update + System.AdminSettings (configuración
/// crítica): el código es inmutable y el desactivado es soft (IsActive).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ProfessionalCatalogsController(IMediator mediator, ICurrentContext context)
    : ControllerBase
{
    /// <summary>Catálogo de profesiones con sus especialidades válidas.</summary>
    [HttpGet("professional-types")]
    public async Task<ActionResult<IReadOnlyList<ProfessionalTypeDto>>> ProfessionalTypes(
        CancellationToken ct
    )
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

    [HttpPost("professional-types")]
    [RequirePermission(PermissionCodes.ProfessionalsUpdate)]
    public async Task<ActionResult<ProfessionalTypeDto>> CreateProfessionalType(
        [FromBody] CreateProfessionalTypeRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("System.AdminSettings", ct))
            return Forbid();

        var created = await mediator.Send(
            new CreateProfessionalTypeCommand(
                request.Code,
                request.Name,
                request.Description,
                request.SortOrder
            ),
            ct
        );
        return CreatedAtAction(nameof(ProfessionalTypes), created);
    }

    [HttpPut("professional-types/{id:guid}")]
    [RequirePermission(PermissionCodes.ProfessionalsUpdate)]
    public async Task<ActionResult<ProfessionalTypeDto>> UpdateProfessionalType(
        Guid id,
        [FromBody] UpdateProfessionalTypeRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("System.AdminSettings", ct))
            return Forbid();

        var updated = await mediator.Send(
            new UpdateProfessionalTypeCommand(
                id,
                request.Name,
                request.Description,
                request.SortOrder,
                request.IsActive
            ),
            ct
        );
        return updated is null
            ? NotFound(new { message = "Tipo de profesional no encontrado" })
            : Ok(updated);
    }

    [HttpPost("specialties")]
    [RequirePermission(PermissionCodes.ProfessionalsUpdate)]
    public async Task<ActionResult<SpecialtyDto>> CreateSpecialty(
        [FromBody] CreateSpecialtyRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("System.AdminSettings", ct))
            return Forbid();

        var created = await mediator.Send(
            new CreateSpecialtyCommand(
                request.Code,
                request.Name,
                request.Category,
                request.Description,
                request.SortOrder
            ),
            ct
        );
        return CreatedAtAction(nameof(Specialties), created);
    }

    [HttpPut("specialties/{id:guid}")]
    [RequirePermission(PermissionCodes.ProfessionalsUpdate)]
    public async Task<ActionResult<SpecialtyDto>> UpdateSpecialty(
        Guid id,
        [FromBody] UpdateSpecialtyRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("System.AdminSettings", ct))
            return Forbid();

        var updated = await mediator.Send(
            new UpdateSpecialtyCommand(
                id,
                request.Name,
                request.Category,
                request.Description,
                request.SortOrder,
                request.IsActive
            ),
            ct
        );
        return updated is null
            ? NotFound(new { message = "Especialidad no encontrada" })
            : Ok(updated);
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
        CancellationToken ct = default
    )
    {
        var result = await mediator.Send(
            new ListProfessionalsCatalogQuery(
                page,
                pageSize,
                search,
                status,
                specialtyId,
                locationId,
                organizationId,
                clinicId
            ),
            ct
        );
        return Ok(result);
    }
}

public record CreateProfessionalTypeRequest(
    string Code,
    string Name,
    string? Description,
    int SortOrder = 0
);

public record UpdateProfessionalTypeRequest(
    string? Name,
    string? Description,
    int? SortOrder,
    bool? IsActive
);

public record CreateSpecialtyRequest(
    string Code,
    string Name,
    string Category,
    string? Description,
    int SortOrder = 0
);

public record UpdateSpecialtyRequest(
    string? Name,
    string? Category,
    string? Description,
    int? SortOrder,
    bool? IsActive
);
