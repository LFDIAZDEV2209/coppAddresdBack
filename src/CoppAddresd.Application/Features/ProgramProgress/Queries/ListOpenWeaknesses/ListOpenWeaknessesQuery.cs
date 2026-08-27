using CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListOpenWeaknesses;

/// <summary>
/// Cola clínica de debilidades ABERTAS (SPEC §21, D): listado paginado
/// (default 20, orden ascendente FIFO por <c>detected_at</c> — la más antigua
/// primero) de los hallazgos <c>status = open</c> que esperan decisión de un
/// clínico. Es la vista previa de <c>POST /api/v1/program/weaknesses/{{id}}/status</c>.
/// La API lo protege con <c>Program.Adapt</c> (permiso de decisión clínica).
/// </summary>
public sealed record ListOpenWeaknessesQuery(int Page = 1, int PageSize = 20)
    : IRequest<PaginatedWeaknessesResult>;

/// <summary>Orquesta la lectura de la cola de abiertas (proyección, sin N+1).</summary>
public sealed class ListOpenWeaknessesQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListOpenWeaknessesQuery, PaginatedWeaknessesResult>
{
    public async Task<PaginatedWeaknessesResult> Handle(
        ListOpenWeaknessesQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListOpenWeaknessesAsync(
            request.Page, request.PageSize, ct);

        return new PaginatedWeaknessesResult(
            items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)Math.Max(1, request.PageSize)));
    }
}