using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListOpenInterventions;

/// <summary>
/// Cola clínica de intervenciones ABERTAS (SPEC §22, D): listado paginado
/// (default 20, orden ascendente FIFO por <c>createdAt</c> — la más antigua
/// primero) de las intervenciones con <c>status != 'completed'</c>. Es la vista
/// previa de <c>POST /api/v1/program/interventions/{{id}}/status</c>.
/// La API lo protege con <c>Program.Adapt</c>.
/// </summary>
public sealed record ListOpenInterventionsQuery(int Page = 1, int PageSize = 20)
    : IRequest<PaginatedInterventionsResult>;

/// <summary>Orquesta la lectura de la cola de intervenciones abiertas (proyección, sin N+1).</summary>
public sealed class ListOpenInterventionsQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListOpenInterventionsQuery, PaginatedInterventionsResult>
{
    public async Task<PaginatedInterventionsResult> Handle(
        ListOpenInterventionsQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListOpenInterventionsAsync(
            request.Page, request.PageSize, ct);

        return new PaginatedInterventionsResult(
            items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)Math.Max(1, request.PageSize)));
    }
}
