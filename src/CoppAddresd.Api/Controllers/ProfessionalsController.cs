using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Flujo profesional del ERP: creación orquestada (empleado + invitación +
/// scopes por clínica) y gestión de permisos por contexto. El frontend nunca
/// llama al Auth Service para esto: el backend orquesta los endpoints internos.
/// </summary>
[ApiController]
[Route("api/v1/professionals")]
[Authorize]
public class ProfessionalsController(
    IMediator mediator,
    IEmployeeRepository employees,
    IAuthScopedAssignmentsClient scopedAssignments,
    ILogger<ProfessionalsController> logger
) : ControllerBase
{
    /// <summary>
    /// Estadísticas del directorio con el mismo alcance del listado
    /// (clínica/organización opcionales): totales por estado y desglose por
    /// tipo de profesional para las StatCards.
    /// </summary>
    [HttpGet("stats")]
    [RequirePermission(PermissionCodes.ProfessionalsView)]
    public async Task<ActionResult<EmployeeStatsDto>> Stats(
        [FromQuery] Guid? organizationId = null,
        [FromQuery] Guid? clinicId = null,
        CancellationToken ct = default
    )
    {
        var stats = await mediator.Send(new GetEmployeesStatsQuery(organizationId, clinicId), ct);
        return Ok(stats);
    }

    /// <summary>
    /// Crea el profesional de extremo a extremo: empleado + extensión clínica
    /// + clínicas + (invitación con correo) + (roles/permisos scoped por
    /// clínica). Compensa si falla la aplicación de scopes.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCodes.ProfessionalsCreate)]
    public async Task<ActionResult<CreateProfessionalResult>> Create(
        [FromBody] CreateProfessionalRequest request,
        CancellationToken ct
    )
    {
        var grantedBy = GetCallerId();

        var result = await mediator.Send(
            new CreateProfessionalCommand(
                request.OrganizationId,
                request.FirstName,
                request.MiddleName,
                request.LastName,
                request.Email,
                request.PhoneCountryCode,
                request.PhoneNumber,
                request.JobTitle,
                request.HireDate,
                request.ProfessionalTypeId,
                request.Bio,
                request.Clinics,
                request.SpecialtyIds,
                request.Licenses,
                request.ScopedRoles,
                request.ScopedPermissions,
                request.SendInvitation,
                grantedBy
            ),
            ct
        );

        return CreatedAtAction(nameof(GetScopes), new { id = result.EmployeeId }, result);
    }

    /// <summary>Asignaciones scoped actuales del profesional (roles + overrides por clínica).</summary>
    [HttpGet("{id:guid}/scopes")]
    [RequirePermission(PermissionCodes.ProfessionalsView)]
    public async Task<ActionResult<object>> GetScopes(Guid id, CancellationToken ct)
    {
        var (employee, scopes) = await LoadScopesAsync(id, ct);
        if (employee is null)
            return NotFound(new { message = "Profesional no encontrado" });

        return Ok(scopes);
    }

    /// <summary>Reemplaza las asignaciones scoped del profesional (roles + overrides por clínica).</summary>
    [HttpPut("{id:guid}/scopes")]
    [RequirePermission(PermissionCodes.ProfessionalsUpdate)]
    public async Task<ActionResult<object>> UpdateScopes(
        Guid id,
        [FromBody] UpdateProfessionalScopesRequest request,
        CancellationToken ct
    )
    {
        var (employee, _) = await LoadScopesAsync(id, ct);
        if (employee is null)
            return NotFound(new { message = "Profesional no encontrado" });

        if (employee.UserId is null)
            return BadRequest(
                new
                {
                    message = "El profesional aún no tiene usuario vinculado. Invítalo primero para poder asignar permisos por clínica.",
                }
            );

        await scopedAssignments.ReplaceAsync(
            employee.UserId.Value,
            request.Roles,
            request.Permissions,
            GetCallerId(),
            ct
        );

        logger.LogInformation(
            "Scopes del profesional {EmployeeId} reemplazados ({Roles} roles, {Permissions} overrides)",
            id,
            request.Roles.Count,
            request.Permissions.Count
        );

        return NoContent();
    }

    private async Task<(Employee? Employee, object? Scopes)> LoadScopesAsync(
        Guid id,
        CancellationToken ct
    )
    {
        var employee = await employees.GetByIdAsync(id, ct);
        if (employee is null)
            return (null, null);

        if (employee.UserId is null)
        {
            return (
                employee,
                new
                {
                    roles = Array.Empty<object>(),
                    permissions = Array.Empty<object>(),
                    requiresInvitation = true,
                }
            );
        }

        var snapshot = await scopedAssignments.GetAsync(employee.UserId.Value, ct);

        return (
            employee,
            new
            {
                requiresInvitation = false,
                roles = snapshot.Roles.Select(r => new
                {
                    r.RoleId,
                    r.RoleName,
                    r.ScopeType,
                    r.ScopeId,
                }),
                permissions = snapshot.Permissions.Select(p => new
                {
                    p.PermissionId,
                    p.PermissionCode,
                    p.ScopeType,
                    p.ScopeId,
                    p.Effect,
                }),
            }
        );
    }

    /// <summary>Horarios semanales de atención del profesional.</summary>
    [HttpGet("{id:guid}/schedules")]
    [RequirePermission(PermissionCodes.ProfessionalsView)]
    public async Task<ActionResult<IReadOnlyList<ProfessionalScheduleDto>>> GetSchedules(
        Guid id,
        CancellationToken ct
    )
    {
        var schedules = await mediator.Send(
            new GetProfessionalSchedulesQuery(id),
            ct
        );

        return Ok(schedules);
    }

    /// <summary>Reemplaza los horarios semanales de atención del profesional.</summary>
    [HttpPut("{id:guid}/schedules")]
    [RequirePermission(PermissionCodes.ProfessionalsUpdate)]
    public async Task<IActionResult> UpdateSchedules(
        Guid id,
        [FromBody] PutSchedulesRequest request,
        CancellationToken ct
    )
    {
        await mediator.Send(
            new PutProfessionalSchedulesCommand(id, request.Schedules),
            ct
        );

        return NoContent();
    }

    private Guid? GetCallerId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record CreateProfessionalRequest(
    Guid OrganizationId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? JobTitle,
    DateOnly? HireDate,
    Guid? ProfessionalTypeId,
    string? Bio,
    IReadOnlyList<ClinicAssignmentInput>? Clinics,
    IReadOnlyList<Guid>? SpecialtyIds,
    IReadOnlyList<LicenseInput>? Licenses,
    IReadOnlyList<ScopedRoleAssignmentInput>? ScopedRoles,
    IReadOnlyList<ScopedPermissionAssignmentInput>? ScopedPermissions,
    bool SendInvitation = true
);

public record UpdateProfessionalScopesRequest(
    IReadOnlyList<ScopedRoleAssignmentInput> Roles,
    IReadOnlyList<ScopedPermissionAssignmentInput> Permissions
);

public record PutSchedulesRequest(IReadOnlyList<ScheduleSlotInput> Schedules);
