using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListEnrollments;

/// <summary>
/// Consulta paginada del listado de inscripciones (SPEC §7.5, ERP, permiso
/// <c>Program.View</c> en la API). Filtros opcionales por paciente y estado.
/// <paramref name="ScopedPatientIds"/> (T-81): ids visibles por el actor —
/// <c>null</c> = sin filtro (Admin/org-clinic admin); lista con ids = filtro
/// IN; lista VACÍA = denegar (resultado sin filas).
/// </summary>
public sealed record ListEnrollmentsQuery(
    Guid? PatientId = null,
    ProgramEnrollmentStatus? Status = null,
    int Page = 1,
    int PageSize = 20,
    IReadOnlyList<Guid>? ScopedPatientIds = null) : IRequest<PaginatedEnrollmentsResult>;

/// <summary>Pagina el listado y normaliza page/pageSize (máx 100, SPEC §6).</summary>
public sealed class ListEnrollmentsQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListEnrollmentsQuery, PaginatedEnrollmentsResult>
{
    public async Task<PaginatedEnrollmentsResult> Handle(ListEnrollmentsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // T-81: sin ids visibles → denegar sin tocar la BD (anti-IDOR).
        if (request.ScopedPatientIds is { Count: 0 })
        {
            return new PaginatedEnrollmentsResult([], 0, page, pageSize, 1);
        }

        var (items, total) = await repository.ListEnrollmentsAsync(
            request.PatientId, request.Status, page, pageSize, request.ScopedPatientIds, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PaginatedEnrollmentsResult(items, total, page, pageSize, totalPages);
    }
}