using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Application.Features.Professionals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Directorio de empleados del ERP (núcleo HR + extensión clínica). La
/// creación del usuario en el Auth Service y la invitación llegan en Fase 3;
/// hoy el empleado se crea con <c>userId = null</c> y estado Invited.
/// </summary>
[ApiController]
[Route("api/v1/employees")]
[Authorize]
public class EmployeesController(IMediator mediator) : ControllerBase
{
    [HttpPut("{id:guid}/access")]
    [RequirePermission(PermissionCodes.EmployeesUpdate)]
    public async Task<IActionResult> ChangeAccess(Guid id, ChangeProfessionalAccessRequest request, CancellationToken ct)
    {
        if (!User.HasClaim("aud", "erp") || !Guid.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actor))
            return Forbid();
        var result = await mediator.Send(new ChangeProfessionalAccessCommand(id, actor, request.OperationId, request.Status), ct);
        return StatusCode(result.Pending ? StatusCodes.Status202Accepted : StatusCodes.Status200OK, result);
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.EmployeesView)]
    public async Task<ActionResult<PaginatedEmployeesResult>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] Guid? clinicId = null,
        [FromQuery] Guid? specialtyId = null,
        [FromQuery] Guid? roleId = null,
        CancellationToken ct = default
    )
    {
        var result = await mediator.Send(
            new ListEmployeesQuery(
                page,
                pageSize,
                search,
                status,
                organizationId,
                clinicId,
                specialtyId,
                roleId
            ),
            ct
        );
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken ct)
    {
        var employee = await mediator.Send(new GetEmployeeQuery(id), ct);
        if (employee is null)
            return NotFound(new { message = "Empleado no encontrado" });

        return Ok(employee);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.EmployeesCreate)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken ct
    )
    {
        var command = new CreateEmployeeCommand(
            request.OrganizationId,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.Email,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.JobTitle,
            request.Department,
            request.HireDate,
            request.Status,
            request.ProfessionalTypeId,
            request.Bio,
            request.Clinics,
            request.SpecialtyIds,
            request.Licenses
        );

        var employee = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
    }

    /// <summary>
    /// Creación masiva de empleados desde CSV. Cada fila se procesa de forma
    /// independiente: las válidas se crean aunque otras fallen. Devuelve
    /// resultado por fila (created/skipped). Sin clínicas, sin invitaciones.
    /// </summary>
    [HttpPost("bulk")]
    [RequirePermission(PermissionCodes.EmployeesCreate)]
    public async Task<ActionResult<BulkCreateResultDto>> BulkCreate(
        [FromBody] BulkCreateEmployeesApiRequest request,
        CancellationToken ct
    )
    {
        var rows = request.Rows
            .Select(r => new BulkEmployeeRowInput(
                r.FirstName,
                r.LastName,
                r.Email,
                r.ProfessionalTypeName,
                r.Status))
            .ToList();

        var command = new BulkCreateEmployeesCommand(request.OrganizationId, rows);
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Invita al empleado: crea su usuario en el Auth Service y le envía el
    /// enlace de primer acceso. En dev (email provider Log) la respuesta trae
    /// el enlace; en producción llega solo por correo.
    /// </summary>
    [HttpPost("{id:guid}/invite")]
    [RequirePermission(PermissionCodes.EmployeesCreate)]
    public async Task<ActionResult<InviteEmployeeResult>> Invite(Guid id, CancellationToken ct)
    {
        var invitedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var result = await mediator.Send(
            new InviteEmployeeCommand(id, Guid.TryParse(invitedBy, out var caller) ? caller : null),
            ct
        );
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.EmployeesUpdate)]
    public async Task<ActionResult<EmployeeDto>> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken ct
    )
    {
        var command = new UpdateEmployeeCommand(
            id,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.Email,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.JobTitle,
            request.Department,
            request.HireDate,
            request.Status,
            request.ProfessionalTypeId,
            request.Bio,
            request.Clinics,
            request.SpecialtyIds,
            request.Licenses,
            request.RemoveProfessionalExtension
        );

        var updated = await mediator.Send(command, ct);
        if (updated is null)
            return NotFound(new { message = "Empleado no encontrado" });

        return Ok(updated);
    }
}

public sealed record ChangeProfessionalAccessRequest(Guid OperationId, string Status);

public record CreateEmployeeRequest(
    Guid OrganizationId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? JobTitle,
    string? Department,
    DateOnly? HireDate,
    string? Status,
    Guid? ProfessionalTypeId,
    string? Bio,
    IReadOnlyList<ClinicAssignmentInput>? Clinics,
    IReadOnlyList<Guid>? SpecialtyIds,
    IReadOnlyList<LicenseInput>? Licenses
);

public record UpdateEmployeeRequest(
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Email,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? JobTitle,
    string? Department,
    DateOnly? HireDate,
    string? Status,
    Guid? ProfessionalTypeId,
    string? Bio,
    IReadOnlyList<ClinicAssignmentInput>? Clinics,
    IReadOnlyList<Guid>? SpecialtyIds,
    IReadOnlyList<LicenseInput>? Licenses,
    bool RemoveProfessionalExtension = false
);

/// <summary>Fila individual de la solicitud de creación masiva.</summary>
public record BulkEmployeeRowApiRequest(
    string FirstName,
    string LastName,
    string Email,
    string? ProfessionalTypeName,
    string Status);

/// <summary>Solicitud de creación masiva de empleados.</summary>
public record BulkCreateEmployeesApiRequest(
    Guid OrganizationId,
    IReadOnlyList<BulkEmployeeRowApiRequest> Rows);
