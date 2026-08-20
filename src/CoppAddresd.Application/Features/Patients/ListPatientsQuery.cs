using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Lista paginada del directorio de pacientes con filtros opcionales
/// (búsqueda por nombre/MRN/documento/correo, estado, aseguradora y clínica).
/// </summary>
public record ListPatientsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    Guid? InsurerId = null,
    Guid? ClinicId = null)
    : IRequest<PaginatedPatientsResult>;

public sealed class ListPatientsQueryHandler(
    IPatientRepository repository) : IRequestHandler<ListPatientsQuery, PaginatedPatientsResult>
{
    public async Task<PaginatedPatientsResult> Handle(ListPatientsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        var (items, total) = await repository.ListAsync(
            page, pageSize, search, request.Status, request.InsurerId, request.ClinicId, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedPatientsResult(
            items.Select(PatientListItemDto.FromEntity).ToList(),
            total,
            page,
            pageSize,
            totalPages);
    }
}