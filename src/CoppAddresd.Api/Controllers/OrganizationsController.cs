using CoppAddresd.Application.Features.Professionals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Estructura organizacional del ERP: organizaciones, clínicas y sedes.
/// Solo lectura del árbol y mutaciones administrativas básicas (Fase 1);
/// los permisos por scope llegan en Fase 2.
/// </summary>
[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public class OrganizationsController(IMediator mediator) : ControllerBase
{
    /// <summary>Árbol completo organización → clínicas → sedes.</summary>
    [HttpGet("tree")]
    public async Task<ActionResult<IReadOnlyList<OrganizationTreeNodeDto>>> Tree(CancellationToken ct)
    {
        var tree = await mediator.Send(new ListOrganizationTreeQuery(), ct);
        return Ok(tree);
    }

    [HttpPost]
    public async Task<ActionResult<OrganizationDto>> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken ct)
    {
        var created = await mediator.Send(new CreateOrganizationCommand(request.Code, request.Name), ct);
        return CreatedAtAction(nameof(Tree), new { }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrganizationDto>> Update(
        Guid id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken ct)
    {
        var updated = await mediator.Send(
            new UpdateOrganizationCommand(id, request.Name, request.IsActive), ct);
        if (updated is null)
            return NotFound(new { message = "Organización no encontrada" });

        return Ok(updated);
    }

    [HttpPost("clinics")]
    public async Task<ActionResult<ClinicDto>> CreateClinic(
        [FromBody] CreateClinicRequest request,
        CancellationToken ct)
    {
        var created = await mediator.Send(
            new CreateClinicCommand(request.OrganizationId, request.Name, request.Code), ct);
        return CreatedAtAction(nameof(Tree), new { }, created);
    }

    [HttpPut("clinics/{id:guid}")]
    public async Task<ActionResult<ClinicDto>> UpdateClinic(
        Guid id,
        [FromBody] UpdateClinicRequest request,
        CancellationToken ct)
    {
        var updated = await mediator.Send(
            new UpdateClinicCommand(id, request.Name, request.Code, request.IsActive), ct);
        if (updated is null)
            return NotFound(new { message = "Clínica no encontrada" });

        return Ok(updated);
    }

    [HttpPost("locations")]
    public async Task<ActionResult<LocationDto>> CreateLocation(
        [FromBody] CreateLocationRequest request,
        CancellationToken ct)
    {
        var created = await mediator.Send(new CreateLocationCommand(
            request.ClinicId,
            request.Name,
            request.AddressLine1,
            request.AddressLine2,
            request.CityId,
            request.StateId,
            request.PostalCode,
            request.PhoneCountryCode,
            request.PhoneNumber), ct);
        return CreatedAtAction(nameof(Tree), new { }, created);
    }

    [HttpPut("locations/{id:guid}")]
    public async Task<ActionResult<LocationDto>> UpdateLocation(
        Guid id,
        [FromBody] UpdateLocationRequest request,
        CancellationToken ct)
    {
        var updated = await mediator.Send(new UpdateLocationCommand(
            id,
            request.Name,
            request.AddressLine1,
            request.AddressLine2,
            request.CityId,
            request.StateId,
            request.PostalCode,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.IsActive), ct);
        if (updated is null)
            return NotFound(new { message = "Sede no encontrada" });

        return Ok(updated);
    }
}

public record CreateOrganizationRequest(string Code, string Name);

public record UpdateOrganizationRequest(string? Name, bool? IsActive);

public record CreateClinicRequest(Guid OrganizationId, string Name, string? Code);

public record UpdateClinicRequest(string? Name, string? Code, bool? IsActive);

public record CreateLocationRequest(
    Guid ClinicId,
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    Guid? CityId,
    Guid? StateId,
    string? PostalCode,
    string? PhoneCountryCode,
    string? PhoneNumber);

public record UpdateLocationRequest(
    string? Name,
    string? AddressLine1,
    string? AddressLine2,
    Guid? CityId,
    Guid? StateId,
    string? PostalCode,
    string? PhoneCountryCode,
    string? PhoneNumber,
    bool? IsActive);
