using CoppAddresd.Api.Context;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Contexto del usuario autenticado para el switcher de clínica: organización,
/// clínicas asignadas (con sedes) y, por clínica, los permisos efectivos
/// (globales + scoped). El frontend usa esto para mostrar el selector de
/// contexto y para gatear la UI según la clínica activa.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MyContextController(
    IEmployeeRepository employees,
    IScopedAuthorizationClient scopedClient,
    ICurrentContext current) : ControllerBase
{
    [HttpGet("context")]
    public async Task<ActionResult<MyContextDto>> Context(CancellationToken ct)
    {
        if (current.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var employee = await employees.GetByUserIdAsync(userId, ct);
        if (employee is null)
        {
            // Usuario sin empleado vinculado: solo permisos globales (ej. SuperAdmin).
            return Ok(new MyContextDto(
                userId,
                null,
                null,
                null,
                false,
                null,
                true,
                null,
                [],
                current.ActiveClinicId));
        }

        var organization = new MyOrganizationDto(employee.OrganizationId, employee.Organization.Name);

        var clinics = new List<MyClinicDto>();
        foreach (var assignment in employee.ClinicAssignments
                     .Where(a => a.Status == "Active")
                     .OrderByDescending(a => a.IsPrimary))
        {
            var clinic = assignment.Clinic;
            var chain = new List<ScopeEntry>
            {
                new("Clinic", clinic.Id),
                new("Organization", employee.OrganizationId),
                ScopeEntry.Global,
            };

            var permissions = await scopedClient.GetEffectivePermissionsAsync(
                userId, current.SecurityStamp ?? string.Empty, chain, ct);

            clinics.Add(new MyClinicDto(
                clinic.Id,
                clinic.Name,
                assignment.IsPrimary,
                clinic.Locations
                    .Where(l => l.IsActive)
                    .OrderBy(l => l.Name)
                    .Select(l => new MyLocationDto(l.Id, l.Name, l.City?.Name, l.State?.Code))
                    .ToList(),
                permissions));
        }

        return Ok(new MyContextDto(
            userId,
            employee.Id,
            employee.Professional is null ? null : employee.Professional.Id,
            organization,
            employee.Professional is not null,
            employee.Professional?.ProfessionalType?.Name,
            employee.Professional?.OnboardingCompletedAt is not null,
            employee.Status,
            clinics,
            current.ActiveClinicId));
    }
}

public record MyOrganizationDto(Guid Id, string Name);

public record MyLocationDto(Guid Id, string Name, string? CityName, string? StateCode);

public record MyClinicDto(
    Guid Id,
    string Name,
    bool IsPrimary,
    IReadOnlyList<MyLocationDto> Locations,
    IReadOnlyList<string> Permissions);

public record MyContextDto(
    Guid UserId,
    Guid? EmployeeId,
    Guid? ProfessionalId,
    MyOrganizationDto? Organization,
    bool IsProfessional,
    string? ProfessionalTypeName,
    bool OnboardingCompleted,
    string? EmployeeStatus,
    IReadOnlyList<MyClinicDto> Clinics,
    Guid? ActiveClinicId);
