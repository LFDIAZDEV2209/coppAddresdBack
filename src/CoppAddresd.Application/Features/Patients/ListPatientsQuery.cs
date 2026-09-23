using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Lista paginada del directorio de pacientes con filtros opcionales
/// (búsqueda por nombre/MRN/documento/correo, estado, aseguradora y clínica)
/// y orden server-side por columna (<paramref name="SortBy"/> de la whitelist,
/// <paramref name="SortDir"/> asc/desc; default CreatedAt desc).
/// <paramref name="OwnProfessionalId"/> restringe el resultado al alcance
/// "propio" del profesional (solo pacientes asignados a él): el id se resuelve
/// en el backend desde la identidad del JWT, nunca se acepta del cliente.
/// </summary>
public record ListPatientsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    Guid? InsurerId = null,
    Guid? ClinicId = null,
    Guid? OwnProfessionalId = null,
    string? SortBy = null,
    string? SortDir = null,
    string? StateCode = null
) : IRequest<PaginatedPatientsResult>;

/// <summary>Campos de orden permitidos del listado (whitelist anti-inyección).</summary>
public static class PatientSortFields
{
    public const string FirstName = "firstName";
    public const string DocumentNumber = "documentNumber";
    public const string PhoneNumber = "phoneNumber";
    public const string InsurerName = "insurerName";
    public const string ClinicName = "clinicName";
    public const string Status = "status";
    public const string CreatedAt = "createdAt";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        FirstName,
        DocumentNumber,
        PhoneNumber,
        InsurerName,
        ClinicName,
        Status,
        CreatedAt,
    };
}

public sealed class ListPatientsQueryHandler(IPatientRepository repository)
    : IRequestHandler<ListPatientsQuery, PaginatedPatientsResult>
{
    public async Task<PaginatedPatientsResult> Handle(
        ListPatientsQuery request,
        CancellationToken ct
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var sortBy = request.SortBy is { } requestedSortBy
            && PatientSortFields.All.Contains(requestedSortBy)
            ? requestedSortBy
            : null;
        var sortDir = string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";
        var stateCode = string.IsNullOrWhiteSpace(request.StateCode)
            ? null
            : request.StateCode.Trim().ToUpperInvariant();

        var (items, total) = await repository.ListAsync(
            page,
            pageSize,
            search,
            request.Status,
            request.InsurerId,
            request.ClinicId,
            request.OwnProfessionalId,
            sortBy,
            sortDir,
            stateCode,
            ct
        );

        // Nombres de profesionales de la página: ids únicos de asignaciones
        // activas → una sola consulta agrupada (nunca una por fila).
        var professionalIds = items
            .SelectMany(p => p.Assignments)
            .Where(a => a.Status == "Active")
            .Select(a => a.ProfessionalId)
            .Distinct()
            .ToList();

        var professionalNames =
            professionalIds.Count > 0
                ? await repository.GetProfessionalNamesAsync(professionalIds, ct)
                : new Dictionary<Guid, string>();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedPatientsResult(
            items.Select(p => PatientListItemDto.FromEntity(p, professionalNames)).ToList(),
            total,
            page,
            pageSize,
            totalPages
        );
    }
}
