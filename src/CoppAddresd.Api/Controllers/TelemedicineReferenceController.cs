using CoppAddresd.Api.Authorization;
using CoppAddresd.Application.Features.Telemedicine;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Datos de referencia para el microservicio de Telemedicina (schema <c>tele</c>
/// usa referencias débiles por Id al ERP). Endpoints internos protegidos con
/// <c>X-Internal-Key</c> (ver <see cref="RequireInternalKeyAttribute"/>): el
/// microservicio NO posee ni gestiona los datos maestros del ERP. Sin JWT: la
/// credencial es la clave interna compartida.
/// </summary>
[ApiController]
[Route("api/v1/internal/telemedicine")]
[RequireInternalKey]
public class TelemedicineReferenceController(IMediator mediator) : ControllerBase
{
    /// <summary>Profesional por id de su extensión clínica (<c>erp.professionals</c>).</summary>
    [HttpGet("professionals/{id:guid}")]
    [ProducesResponseType(typeof(TelemedicineProfessionalRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemedicineProfessionalRefDto>> GetProfessional(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTelemedicineProfessionalRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Profesional no encontrado." }) : Ok(result);
    }

    /// <summary>Paciente por id (<c>app.patient_profiles</c>).</summary>
    [HttpGet("patients/{id:guid}")]
    [ProducesResponseType(typeof(TelemedicinePatientRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemedicinePatientRefDto>> GetPatient(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTelemedicinePatientRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Paciente no encontrado." }) : Ok(result);
    }

    /// <summary>Especialidad por id (<c>erp.specialties</c>).</summary>
    [HttpGet("specialties/{id:guid}")]
    [ProducesResponseType(typeof(TelemedicineSpecialtyRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemedicineSpecialtyRefDto>> GetSpecialty(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTelemedicineSpecialtyRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Especialidad no encontrada." }) : Ok(result);
    }

    /// <summary>Sede por id (<c>erp.locations</c>).</summary>
    [HttpGet("locations/{id:guid}")]
    [ProducesResponseType(typeof(TelemedicineLocationRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemedicineLocationRefDto>> GetLocation(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTelemedicineLocationRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Sede no encontrada." }) : Ok(result);
    }
}
