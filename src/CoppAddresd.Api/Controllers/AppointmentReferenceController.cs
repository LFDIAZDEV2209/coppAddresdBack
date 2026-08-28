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
public class AppointmentReferenceController(IMediator mediator) : ControllerBase
{
    /// <summary>Profesional por id de su extensión clínica (<c>erp.professionals</c>).</summary>
    [HttpGet("professionals/{id:guid}")]
    [ProducesResponseType(typeof(AppointmentProfessionalRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentProfessionalRefDto>> GetProfessional(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAppointmentProfessionalRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Profesional no encontrado." }) : Ok(result);
    }

    /// <summary>Paciente por id (<c>app.patient_profiles</c>).</summary>
    [HttpGet("patients/{id:guid}")]
    [ProducesResponseType(typeof(AppointmentPatientRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentPatientRefDto>> GetPatient(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAppointmentPatientRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Paciente no encontrado." }) : Ok(result);
    }

    /// <summary>Especialidad por id (<c>erp.specialties</c>).</summary>
    [HttpGet("specialties/{id:guid}")]
    [ProducesResponseType(typeof(AppointmentSpecialtyRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentSpecialtyRefDto>> GetSpecialty(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAppointmentSpecialtyRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Especialidad no encontrada." }) : Ok(result);
    }

    /// <summary>Sede por id (<c>erp.locations</c>).</summary>
    [HttpGet("locations/{id:guid}")]
    [ProducesResponseType(typeof(AppointmentLocationRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentLocationRefDto>> GetLocation(
        Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAppointmentLocationRefQuery(id), ct);
        return result is null ? NotFound(new { message = "Sede no encontrada." }) : Ok(result);
    }

    /// <summary>
    /// Profesional por usuario de Auth (contexto del JWT). Lo usa el microservicio
    /// para autorizar el acceso a una sala: el profesional de la cita se resuelve
    /// desde el token, nunca desde un id enviado por el cliente.
    /// </summary>
    [HttpGet("professionals/by-user/{userId:guid}")]
    [ProducesResponseType(typeof(AppointmentProfessionalRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentProfessionalRefDto>> GetProfessionalByUser(
        Guid userId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAppointmentProfessionalByUserIdQuery(userId), ct);
        return result is null ? NotFound(new { message = "Profesional no encontrado para el usuario." }) : Ok(result);
    }

    /// <summary>Paciente por usuario de Auth (contexto del JWT), para autorizar el acceso a la sala del paciente.</summary>
    [HttpGet("patients/by-user/{userId:guid}")]
    [ProducesResponseType(typeof(AppointmentPatientRefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentPatientRefDto>> GetPatientByUser(
        Guid userId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAppointmentPatientByUserIdQuery(userId), ct);
        return result is null ? NotFound(new { message = "Paciente no encontrado para el usuario." }) : Ok(result);
    }
}
