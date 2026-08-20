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
    Guid? ClinicId) : IRequest<PaginatedEmployeesResult>;

/// <summary>Resultado paginado del listado de empleados.</summary>
public record PaginatedEmployeesResult(
    IReadOnlyList<EmployeeListItemDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class ListEmployeesQueryHandler(
    IEmployeeRepository repository) : IRequestHandler<ListEmployeesQuery, PaginatedEmployeesResult>
{
    public async Task<PaginatedEmployeesResult> Handle(ListEmployeesQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.Status,
            request.OrganizationId,
            request.ClinicId,
            ct);

        return new PaginatedEmployeesResult(
            items.Select(EmployeeListItemDto.FromEntity).ToList(),
            total,
            request.Page,
            request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }
}

/// <summary>Empleado completo con clínicas y extensión profesional.</summary>
public record GetEmployeeQuery(Guid Id) : IRequest<EmployeeDto?>;

public sealed class GetEmployeeQueryHandler(
    IEmployeeRepository repository) : IRequestHandler<GetEmployeeQuery, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(GetEmployeeQuery request, CancellationToken ct)
    {
        var employee = await repository.GetByIdAsync(request.Id, ct);
        return employee is null ? null : EmployeeDto.FromEntity(employee);
    }
}
