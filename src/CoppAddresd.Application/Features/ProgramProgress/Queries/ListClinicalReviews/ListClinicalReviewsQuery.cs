using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListClinicalReviews;

/// <summary>
/// Cola de revisiones clínicas de XP pendientes (SPEC §15, D): listado paginado
/// de las mejorías significativas que esperan decisión de un clínico
/// (<c>status = 'pending'</c>). La API lo protege con <c>Program.Adapt</c>
/// (permiso de decisión clínica del módulo). Es la vista previa de la acción
/// <c>POST /xp-rules/clinical-pending/{id}/decide</c>.
/// </summary>
public sealed record ListClinicalReviewsQuery(int Page = 1, int PageSize = 20)
    : IRequest<PaginatedClinicalReviewsResult>;

/// <summary>Resultado paginado de la cola de revisiones clínicas (SPEC §15).</summary>
public sealed record PaginatedClinicalReviewsResult(
    IReadOnlyList<ClinicalReviewDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Orquesta la lectura de la cola de pendientes vía el repositorio (proyección, sin N+1).</summary>
public sealed class ListClinicalReviewsQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListClinicalReviewsQuery, PaginatedClinicalReviewsResult>
{
    public async Task<PaginatedClinicalReviewsResult> Handle(
        ListClinicalReviewsQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListPendingClinicalReviewsAsync(
            request.Page, request.PageSize, ct);

        return new PaginatedClinicalReviewsResult(
            items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)Math.Max(1, request.PageSize)));
    }
}