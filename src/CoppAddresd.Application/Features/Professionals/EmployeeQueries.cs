using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Lista paginada del directorio de empleados con filtros.</summary>
public record ListEmployeesQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Status,
    Guid? OrganizationId,
    Guid? ClinicId,
    Guid? SpecialtyId,
    Guid? RoleId
) : IRequest<PaginatedEmployeesResult>;

/// <summary>Resultado paginado del listado de empleados.</summary>
public record PaginatedEmployeesResult(
    IReadOnlyList<EmployeeListItemDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

public sealed class ListEmployeesQueryHandler(
    IEmployeeRepository repository,
    IAuthUsersByRoleClient usersByRole,
    IErpAccessClient access
) : IRequestHandler<ListEmployeesQuery, PaginatedEmployeesResult>
{
    public async Task<PaginatedEmployeesResult> Handle(
        ListEmployeesQuery request,
        CancellationToken ct
    )
    {
        // El filtro por rol vive en el Auth Service (schema auth, dueño el
        // Auth Service): se resuelven los userIds con el rol y se filtra por
        // employee.user_id. El ERP nunca lee auth.* directamente.
        IReadOnlyList<Guid>? userIds = null;
        if (request.RoleId is not null)
        {
            userIds = await usersByRole.GetUserIdsByRoleAsync(request.RoleId.Value, ct);
        }

        var (items, total) = await repository.ListAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.Status,
            request.OrganizationId,
            request.ClinicId,
            request.SpecialtyId,
            userIds,
            ct
        );

        var pending = await access.PendingAsync(items.Select(x => x.Id).ToArray(), ct);
        var pendingByEmployee = pending.ToDictionary(x => x.EmployeeId);
        return new PaginatedEmployeesResult(
            items.Select(x =>
            {
                var item = EmployeeListItemDto.FromEntity(x);
                return pendingByEmployee.TryGetValue(x.Id, out var operation)
                    ? item with { PendingStatus = operation.Status, PendingOperationId = operation.Id }
                    : item;
            }).ToList(),
            total,
            request.Page,
            request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize)
        );
    }
}

/// <summary>
/// Preflight del alta: disponibilidad del correo en la organización (la
/// unicidad del correo de empleados es por organización) + contexto de los
/// perfiles y la cuenta que ya lo usan. Lo consume el wizard al escribir para
/// mostrar la tarjeta de perfil existente sin esperar al envío.
/// </summary>
public record CheckEmployeeEmailAvailabilityQuery(Guid OrganizationId, string Email)
    : IRequest<EmailAvailabilityResult>;

/// <summary>Empleado existente con ese correo en la organización.</summary>
public record EmployeeEmailMatchDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Status,
    bool HasAccount
);

/// <summary>Paciente existente con ese correo (coexistencia de perfiles).</summary>
public record PatientEmailMatchDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Status,
    bool HasAccount
);

/// <summary>
/// Estado de la cuenta de Auth con ese correo. Null cuando el Auth Service no
/// respondió (lectura informativa best-effort).
/// </summary>
public record AccountEmailMatchDto(bool Exists, bool IsActive, bool HasPassword);

/// <summary>
/// Disponibilidad de un correo dentro de una organización + contexto de los
/// perfiles/cuenta que ya lo usan (tarjeta del wizard).
/// </summary>
public record EmailAvailabilityResult(
    bool Available,
    EmployeeEmailMatchDto? Employee = null,
    PatientEmailMatchDto? Patient = null,
    AccountEmailMatchDto? Account = null
);

public sealed class CheckEmployeeEmailAvailabilityQueryHandler(
    IEmployeeRepository repository,
    IPatientRepository patients,
    IAuthUsersLookupClient usersLookup
) : IRequestHandler<CheckEmployeeEmailAvailabilityQuery, EmailAvailabilityResult>
{
    public async Task<EmailAvailabilityResult> Handle(
        CheckEmployeeEmailAvailabilityQuery request,
        CancellationToken ct
    )
    {
        // Entrada incompleta = sin veredicto de conflicto (el alta revalida).
        if (request.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email))
        {
            return new EmailAvailabilityResult(true);
        }

        // Misma normalización que CreateEmployeeCommand: el correo se guarda
        // en minúsculas, así que la comparación es efectivamente insensible a
        // mayúsculas.
        var email = request.Email.Trim().ToLowerInvariant();

        var employee = await repository.GetByEmailAsync(request.OrganizationId, email, ct);
        var patient = await patients.GetByEmailAsync(email, ct);
        var account = await usersLookup.LookupByEmailAsync(email, ct);

        return new EmailAvailabilityResult(
            employee is null,
            employee is null
                ? null
                : new EmployeeEmailMatchDto(
                    employee.Id,
                    employee.FirstName,
                    employee.LastName,
                    employee.Status,
                    employee.UserId is not null
                ),
            patient is null
                ? null
                : new PatientEmailMatchDto(
                    patient.Id,
                    patient.FirstName,
                    patient.LastName,
                    patient.Status,
                    patient.UserId is not null
                ),
            account is null
                ? null
                : new AccountEmailMatchDto(
                    account.Exists,
                    account.IsActive,
                    account.HasPassword
                )
        );
    }
}

/// <summary>Empleado completo con clínicas y extensión profesional.</summary>
public record GetEmployeeQuery(Guid Id) : IRequest<EmployeeDto?>;

public sealed class GetEmployeeQueryHandler(IEmployeeRepository repository)
    : IRequestHandler<GetEmployeeQuery, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(GetEmployeeQuery request, CancellationToken ct)
    {
        var employee = await repository.GetByIdAsync(request.Id, ct);
        return employee is null ? null : EmployeeDto.FromEntity(employee);
    }
}

/// <summary>Desglose por tipo de profesional (para las StatCards del directorio).</summary>
public record EmployeeTypeStatDto(string? ProfessionalTypeName, int Count);

/// <summary>
/// Estadísticas del directorio con el mismo alcance que el listado
/// (clínica/organización opcionales): total, activos, invitados, inactivos y
/// desglose por tipo de profesional. Los "invitados" son empleados con estado
/// Invited (creados sin usuario de Auth aún).
/// </summary>
public record EmployeeStatsDto(
    int Total,
    int Active,
    int Invited,
    int Inactive,
    IReadOnlyList<EmployeeTypeStatDto> ByType
);

/// <summary>Stats del directorio de empleados/profesionales.</summary>
public record GetEmployeesStatsQuery(Guid? OrganizationId, Guid? ClinicId, bool Fresh = false)
    : IRequest<EmployeeStatsDto>;

/// <summary>
/// Cache-aside con TTL 30-60s (jitter anti-stampede), hash de alcance en la
/// clave: organización/clínica distintos nunca comparten key. Staleness
/// máximo = TTL (tolerado por diseño para dashboards).
/// </summary>
public sealed class GetEmployeesStatsQueryHandler(
    IEmployeeRepository repository,
    ICacheService cache
) : IRequestHandler<GetEmployeesStatsQuery, EmployeeStatsDto>
{
    public async Task<EmployeeStatsDto> Handle(GetEmployeesStatsQuery request, CancellationToken ct)
    {
        var scopeHash = CacheKeys.HashScope(
            request.OrganizationId?.ToString(),
            request.ClinicId?.ToString()
        );
        if (request.Fresh)
        {
            var current = await repository.GetStatsAsync(request.OrganizationId, request.ClinicId, ct);
            await cache.SetAsync(CacheKeys.Stats("employees", scopeHash), current, CacheKeys.StatsTtl(), ct);
            return current;
        }
        return await cache.GetOrCreateAsync(
            CacheKeys.Stats("employees", scopeHash),
            CacheKeys.StatsTtl(),
            token => repository.GetStatsAsync(request.OrganizationId, request.ClinicId, token),
            ct
        );
    }
}
