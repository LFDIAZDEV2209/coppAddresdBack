using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>
/// Catálogo de profesionales clínicos (empleados con extensión clínica) para
/// la UI de Telemedicina: elegir profesional al crear una solicitud y el
/// directorio admin. Sin PHI: solo identidad, profesión, especialidades, sedes
/// y estado. Accesible para cualquier usuario autenticado (como los demás
/// catálogos del ERP: especialidades, profesiones).
/// </summary>
public record ListProfessionalsCatalogQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    Guid? SpecialtyId = null,
    Guid? LocationId = null,
    Guid? OrganizationId = null,
    Guid? ClinicId = null) : IRequest<PaginatedProfessionalsCatalogResult>;

public sealed record PaginatedProfessionalsCatalogResult(
    IReadOnlyList<ProfessionalCatalogItemDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record ProfessionalCatalogItemDto(
    Guid Id,
    Guid EmployeeId,
    string FullName,
    string? ProfessionalTypeName,
    IReadOnlyList<ProfessionalCatalogSpecialtyDto> Specialties,
    IReadOnlyList<ProfessionalCatalogLocationDto> Locations,
    IReadOnlyList<Guid> ClinicIds,
    string Status);

public sealed record ProfessionalCatalogSpecialtyDto(Guid Id, string Name);

public sealed record ProfessionalCatalogLocationDto(Guid Id, string Name);

public static class ProfessionalCatalogItemFactory
{
    public static ProfessionalCatalogItemDto FromEntity(Employee employee) => new(
        employee.Professional!.Id,
        employee.Id,
        $"{employee.FirstName} {employee.MiddleName} {employee.LastName}".Trim(),
        employee.Professional.ProfessionalType?.Name,
        employee.Professional.Specialties
            .OrderBy(s => s.Specialty.Name)
            .Select(s => new ProfessionalCatalogSpecialtyDto(s.SpecialtyId, s.Specialty.Name))
            .ToList(),
        employee.ClinicAssignments
            .Where(a => a.Status == "Active")
            .SelectMany(a => a.Clinic.Locations)
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new ProfessionalCatalogLocationDto(l.Id, l.Name))
            .DistinctBy(l => l.Id)
            .ToList(),
        employee.ClinicAssignments
            .Where(a => a.Status == "Active")
            .Select(a => a.ClinicId)
            .Distinct()
            .ToList(),
        employee.Status);
}

public sealed class ListProfessionalsCatalogQueryHandler(
    IEmployeeRepository repository) : IRequestHandler<ListProfessionalsCatalogQuery, PaginatedProfessionalsCatalogResult>
{
    public async Task<PaginatedProfessionalsCatalogResult> Handle(
        ListProfessionalsCatalogQuery request,
        CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        var (items, total) = await repository.ListProfessionalsAsync(
            page,
            pageSize,
            search,
            request.Status,
            request.SpecialtyId,
            request.LocationId,
            request.OrganizationId,
            request.ClinicId,
            ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedProfessionalsCatalogResult(
            items.Select(ProfessionalCatalogItemFactory.FromEntity).ToList(),
            total,
            page,
            pageSize,
            totalPages);
    }
}
